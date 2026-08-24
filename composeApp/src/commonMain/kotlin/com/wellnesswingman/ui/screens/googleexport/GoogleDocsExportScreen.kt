package com.wellnesswingman.ui.screens.googleexport

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.selection.SelectionContainer
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalUriHandler
import androidx.compose.ui.unit.dp
import cafe.adriel.voyager.core.screen.Screen
import cafe.adriel.voyager.koin.getScreenModel
import cafe.adriel.voyager.navigator.LocalNavigator
import cafe.adriel.voyager.navigator.currentOrThrow
import com.wellnesswingman.domain.report.HealthReportPreset
import com.wellnesswingman.domain.report.HealthReportSection
import com.wellnesswingman.platform.GoogleConsentHost

class GoogleDocsExportScreen : Screen {
    @Composable override fun Content() {
        val navigator = LocalNavigator.currentOrThrow
        val viewModel = getScreenModel<GoogleDocsExportViewModel>()
        val state by viewModel.uiState.collectAsState()
        val uriHandler = LocalUriHandler.current
        GoogleConsentHost { viewModel.resumeAfterConsent() }
        if (state.showConfirmation) AlertDialog(onDismissRequest = viewModel::dismissConfirmation,
            title = { Text("Create Google Doc?") },
            text = { Text("Your selected health data will be sent directly from this device to Google. No tokens or report data are stored by WellnessWingman.") },
            confirmButton = { TextButton(onClick = viewModel::createGoogleDocument) { Text("Create Google Doc") } },
            dismissButton = { TextButton(onClick = viewModel::dismissConfirmation) { Text("Cancel") } })
        Scaffold(topBar = { TopAppBar(title = { Text("Share health diary") }, navigationIcon = { IconButton(onClick = navigator::pop) { Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back") } }) }) { pad ->
            Column(Modifier.fillMaxSize().padding(pad).padding(16.dp).verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Text("Create a date-ranged report for a clinician. Previewed data stays local until you confirm Google upload.", style = MaterialTheme.typography.bodyMedium)
                OutlinedTextField(state.startDate, viewModel::updateStartDate, label = { Text("Start date (YYYY-MM-DD)") }, modifier = Modifier.fillMaxWidth())
                OutlinedTextField(state.endDate, viewModel::updateEndDate, label = { Text("End date (YYYY-MM-DD)") }, modifier = Modifier.fillMaxWidth())
                Text("Captured time zone: ${state.timeZoneId}", style = MaterialTheme.typography.bodySmall)
                Text("Report type", style = MaterialTheme.typography.titleSmall)
                HealthReportPreset.entries.forEach { preset -> Row { RadioButton(selected = state.preset == preset, onClick = { viewModel.selectPreset(preset) }); Text(preset.name.replace('_', ' '), Modifier.padding(top = 12.dp)) } }
                Text("Include", style = MaterialTheme.typography.titleSmall)
                HealthReportSection.entries.forEach { section -> Row { Checkbox(section in state.selectedSections, { viewModel.toggleSection(section) }); Text(section.name.replace('_', ' '), Modifier.padding(top = 12.dp)) } }
                Button(onClick = viewModel::gatherPreview, enabled = !state.isGathering && !state.isExporting, modifier = Modifier.fillMaxWidth()) { Text(if (state.isGathering) "Preparing preview…" else "Prepare local preview") }
                state.preview?.let { preview -> Card(Modifier.fillMaxWidth()) { Text("Preview: ${preview.totalEntries} records selected. This exact local snapshot will be exported.", Modifier.padding(16.dp)) } }
                if (!state.isAuthorizationAvailable) Text("Google Docs authorization is unavailable on this platform.", color = MaterialTheme.colorScheme.error)
                Button(onClick = viewModel::requestConfirmation, enabled = state.snapshot?.isEmpty == false && !state.isExporting && state.isAuthorizationAvailable, modifier = Modifier.fillMaxWidth()) { Text(if (state.isExporting) "Creating document…" else "Create Google Doc") }
                state.error?.let { Text(it, color = MaterialTheme.colorScheme.error) }
                state.resultUrl?.let { url ->
                    Card(Modifier.fillMaxWidth()) {
                        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                            Text("Google Doc created", style = MaterialTheme.typography.titleSmall)
                            Button(onClick = { uriHandler.openUri(url) }) { Text("Open Google Doc") }
                            SelectionContainer {
                                Text(url, color = MaterialTheme.colorScheme.primary, style = MaterialTheme.typography.bodySmall)
                            }
                        }
                    }
                }
            }
        }
    }
}
