package com.wellnesswingman.ui.screens.googleexport

import com.wellnesswingman.data.googleexport.GoogleDocsError
import com.wellnesswingman.domain.googleexport.GoogleAuthResult
import com.wellnesswingman.domain.googleexport.GoogleAuthService
import com.wellnesswingman.domain.googleexport.GoogleDocsExportService
import com.wellnesswingman.domain.googleexport.GoogleExportError
import com.wellnesswingman.domain.report.HealthReportBuilder
import com.wellnesswingman.domain.report.HealthReportData
import com.wellnesswingman.domain.report.HealthReportGatherer
import com.wellnesswingman.domain.report.ReportMealEntry
import io.mockk.Runs
import io.mockk.coEvery
import io.mockk.every
import io.mockk.just
import io.mockk.mockk
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.setMain
import kotlinx.datetime.LocalDate
import kotlinx.datetime.LocalDateTime
import kotlin.test.AfterTest
import kotlin.test.BeforeTest
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

@OptIn(ExperimentalCoroutinesApi::class)
class GoogleDocsExportViewModelTest {

    @BeforeTest
    fun setUp() {
        Dispatchers.setMain(Dispatchers.Unconfined)
    }

    @AfterTest
    fun tearDown() {
        Dispatchers.resetMain()
    }

    private fun viewModel(
        auth: GoogleAuthService,
        export: GoogleDocsExportService = mockk(relaxed = true)
    ): GoogleDocsExportViewModel {
        val gatherer = mockk<HealthReportGatherer>(relaxed = true)
        coEvery { gatherer.gather(any()) } returns nonEmptyData()
        return GoogleDocsExportViewModel(
            gatherer = gatherer,
            builder = HealthReportBuilder(),
            authService = auth,
            exportService = export
        )
    }

    private fun nonEmptyData() = HealthReportData(
        start = LocalDate(2024, 1, 10),
        end = LocalDate(2024, 1, 10),
        timeZoneId = "UTC",
        meals = listOf(
            ReportMealEntry(date = LocalDate(2024, 1, 10), localTime = LocalDateTime(2024, 1, 10, 8, 0))
        )
    )

    private fun grantingAuth(): GoogleAuthService {
        val auth = mockk<GoogleAuthService>(relaxed = true)
        every { auth.isAvailable } returns true
        coEvery { auth.requestAccess() } returns GoogleAuthResult.Granted("token")
        every { auth.clearAccessToken() } just Runs
        return auth
    }

    private fun prepareSnapshot(vm: GoogleDocsExportViewModel) {
        vm.gatherPreview()
        assertTrue(vm.uiState.value.snapshot?.isEmpty == false)
    }

    @Test
    fun `gather with no selected sections shows a distinct selection error`() {
        val vm = viewModel(auth = grantingAuth())
        vm.uiState.value.selectedSections.toList().forEach { vm.toggleSection(it) }
        assertTrue(vm.uiState.value.selectedSections.isEmpty())

        vm.gatherPreview()

        assertEquals("Select at least one report section.", vm.uiState.value.error)
    }

    @Test
    fun `gather with invalid dates keeps the date format error`() {
        val vm = viewModel(auth = grantingAuth())
        vm.updateStartDate("not-a-date")

        vm.gatherPreview()

        assertEquals("Use ISO dates (YYYY-MM-DD), with an end date on or after the start date.", vm.uiState.value.error)
    }

    @Test
    fun `export failure surfaces the categorized Google error message`() {
        val export = mockk<GoogleDocsExportService>(relaxed = true)
        coEvery { export.createAndPopulate(any(), any(), any(), any()) } returns Result.failure(
            GoogleExportError.ApiFailure(GoogleDocsError.RateLimited("limit"))
        )
        val vm = viewModel(auth = grantingAuth(), export = export)
        prepareSnapshot(vm)

        vm.createGoogleDocument()

        assertEquals("Google is rate limiting requests, please wait and retry", vm.uiState.value.error)
    }

    @Test
    fun `export failure with an uncleaned partial document warns about the orphaned doc`() {
        val export = mockk<GoogleDocsExportService>(relaxed = true)
        coEvery { export.createAndPopulate(any(), any(), any(), any()) } returns Result.failure(
            GoogleExportError.ApiFailure(
                GoogleDocsError.RateLimited("limit"),
                partialDocumentId = "doc-1",
                cleanupDeleted = false
            )
        )
        val vm = viewModel(auth = grantingAuth(), export = export)
        prepareSnapshot(vm)

        vm.createGoogleDocument()

        assertEquals(
            "Google Docs export failed and the incomplete document could not be removed. Check Google Drive before retrying.",
            vm.uiState.value.error
        )
    }

    @Test
    fun `failed authorization is surfaced as a retryable error`() {
        val auth = mockk<GoogleAuthService>(relaxed = true)
        every { auth.isAvailable } returns true
        coEvery { auth.requestAccess() } returns GoogleAuthResult.Failed
        every { auth.clearAccessToken() } just Runs
        val vm = viewModel(auth = auth)
        prepareSnapshot(vm)

        vm.createGoogleDocument()

        assertEquals("Google authorization failed. You can retry.", vm.uiState.value.error)
    }

    @Test
    fun `cancelled consent is surfaced without re-authorizing`() {
        val auth = mockk<GoogleAuthService>(relaxed = true)
        every { auth.isAvailable } returns true
        coEvery { auth.requestAccess() } returns GoogleAuthResult.Cancelled
        val vm = viewModel(auth = auth)
        prepareSnapshot(vm)

        vm.resumeAfterConsent()

        assertEquals("Google authorization was cancelled.", vm.uiState.value.error)
    }
}
