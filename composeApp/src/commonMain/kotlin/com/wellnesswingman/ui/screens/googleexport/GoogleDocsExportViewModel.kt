package com.wellnesswingman.ui.screens.googleexport

import cafe.adriel.voyager.core.model.ScreenModel
import cafe.adriel.voyager.core.model.screenModelScope
import com.wellnesswingman.domain.common.DateRange
import com.wellnesswingman.domain.googleexport.GoogleAuthResult
import com.wellnesswingman.domain.googleexport.GoogleAuthService
import com.wellnesswingman.domain.googleexport.GoogleDocsExportService
import com.wellnesswingman.domain.googleexport.GoogleExportError
import com.wellnesswingman.domain.report.HealthReportBuilder
import com.wellnesswingman.domain.report.HealthReportData
import com.wellnesswingman.domain.report.HealthReportGatherer
import com.wellnesswingman.domain.report.HealthReportPreset
import com.wellnesswingman.domain.report.HealthReportPreview
import com.wellnesswingman.domain.report.HealthReportRequest
import com.wellnesswingman.domain.report.HealthReportSection
import com.wellnesswingman.domain.report.toPreview
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.datetime.Clock
import kotlinx.datetime.LocalDate
import kotlinx.datetime.TimeZone
import kotlinx.datetime.todayIn

class GoogleDocsExportViewModel(
    private val gatherer: HealthReportGatherer,
    private val builder: HealthReportBuilder,
    private val authService: GoogleAuthService,
    private val exportService: GoogleDocsExportService
) : ScreenModel {
    private val zone = TimeZone.currentSystemDefault()
    private val today = Clock.System.todayIn(zone)
    private val _uiState = MutableStateFlow(GoogleDocsExportUiState(
        startDate = today.toString(), endDate = today.toString(), timeZoneId = zone.id,
        preset = HealthReportPreset.FOOD_DIARY, selectedSections = HealthReportPreset.FOOD_DIARY.defaultSections(),
        isAuthorizationAvailable = authService.isAvailable
    ))
    val uiState: StateFlow<GoogleDocsExportUiState> = _uiState.asStateFlow()

    fun updateStartDate(value: String) = _uiState.update { it.copy(startDate = value, error = null, preview = null, snapshot = null) }
    fun updateEndDate(value: String) = _uiState.update { it.copy(endDate = value, error = null, preview = null, snapshot = null) }
    fun selectPreset(preset: HealthReportPreset) = _uiState.update {
        it.copy(preset = preset, selectedSections = preset.defaultSections(), error = null, preview = null, snapshot = null)
    }
    fun toggleSection(section: HealthReportSection) = _uiState.update {
        val next = it.selectedSections.toMutableSet().apply { if (!add(section)) remove(section) }
        it.copy(preset = HealthReportPreset.CUSTOM, selectedSections = next, error = null, preview = null, snapshot = null)
    }
    fun dismissConfirmation() = _uiState.update { it.copy(showConfirmation = false) }
    fun clearResult() = _uiState.update { it.copy(resultUrl = null, error = null) }

    fun gatherPreview() = screenModelScope.launch {
        val request = requestOrError() ?: return@launch
        _uiState.update { it.copy(isGathering = true, error = null, resultUrl = null) }
        runCatching { gatherer.gather(request) }
            .onSuccess { data ->
                if (data.isEmpty) _uiState.update { it.copy(isGathering = false, snapshot = data, preview = data.toPreview(), error = "No selected data was found. Google authorization was not started.") }
                else _uiState.update { it.copy(isGathering = false, snapshot = data, preview = data.toPreview()) }
            }
            .onFailure { _uiState.update { state -> state.copy(isGathering = false, error = "Unable to prepare the local preview.") } }
    }

    fun requestConfirmation() = _uiState.update {
        if (it.snapshot == null || it.snapshot.isEmpty) it.copy(error = "Prepare a non-empty local preview first.") else it.copy(showConfirmation = true)
    }

    fun createGoogleDocument() {
        _uiState.update { it.copy(showConfirmation = false) }
        authorizeAndExport()
    }

    fun resumeAfterConsent() = authorizeAndExport()

    private fun authorizeAndExport() = screenModelScope.launch {
        val snapshot = _uiState.value.snapshot ?: return@launch
        _uiState.update { it.copy(isExporting = true, awaitingConsent = false, error = null) }
        when (val auth = authService.requestAccess()) {
            is GoogleAuthResult.Granted -> try {
                val document = builder.build(snapshot, Clock.System.now())
                exportService.createAndPopulate(auth.accessToken, "WellnessWingman health report", document)
                    .onSuccess { result -> _uiState.update { it.copy(isExporting = false, resultUrl = result.url) } }
                    .onFailure { failure ->
                        val message = when {
                            failure is GoogleExportError.ApiFailure &&
                                failure.partialDocumentId != null && !failure.cleanupDeleted ->
                                "Google Docs export failed and the incomplete document could not be removed. Check Google Drive before retrying."
                            failure is GoogleExportError -> failure.toUserMessage()
                            else -> "Google Docs export failed. You can retry safely."
                        }
                        _uiState.update { it.copy(isExporting = false, error = message) }
                    }
            } finally { authService.clearAccessToken() }
            GoogleAuthResult.ConsentRequired -> _uiState.update { it.copy(isExporting = false, awaitingConsent = true) }
            GoogleAuthResult.Cancelled -> _uiState.update { it.copy(isExporting = false, error = "Google authorization was cancelled.") }
            GoogleAuthResult.Denied -> _uiState.update { it.copy(isExporting = false, error = "Google authorization was denied.") }
            GoogleAuthResult.Failed -> _uiState.update { it.copy(isExporting = false, error = "Google authorization failed. You can retry.") }
            GoogleAuthResult.Unavailable -> _uiState.update { it.copy(isExporting = false, error = "Google Docs export is available on Android only.") }
        }
    }

    private fun requestOrError(): HealthReportRequest? {
        val selected = _uiState.value.selectedSections
        if (selected.isEmpty()) {
            _uiState.update { it.copy(error = "Select at least one report section.") }
            return null
        }
        return try {
            HealthReportRequest(DateRange.of(LocalDate.parse(_uiState.value.startDate), LocalDate.parse(_uiState.value.endDate)), zone, _uiState.value.preset, selected)
        } catch (e: Exception) {
            _uiState.update { it.copy(error = "Use ISO dates (YYYY-MM-DD), with an end date on or after the start date.") }
            null
        }
    }
}

data class GoogleDocsExportUiState(
    val startDate: String = "", val endDate: String = "", val timeZoneId: String = "",
    val preset: HealthReportPreset = HealthReportPreset.FOOD_DIARY,
    val selectedSections: Set<HealthReportSection> = emptySet(),
    val snapshot: HealthReportData? = null, val preview: HealthReportPreview? = null,
    val isGathering: Boolean = false, val isExporting: Boolean = false, val awaitingConsent: Boolean = false,
    val showConfirmation: Boolean = false, val resultUrl: String? = null, val error: String? = null,
    val isAuthorizationAvailable: Boolean = false
)
