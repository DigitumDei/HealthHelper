package com.wellnesswingman.ui.screens.settings

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.ArrowForward
import androidx.compose.material.icons.filled.Build
import androidx.compose.material.icons.filled.FavoriteBorder
import androidx.compose.material.icons.filled.Info
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Restaurant
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.unit.dp
import cafe.adriel.voyager.core.screen.Screen
import cafe.adriel.voyager.navigator.LocalNavigator
import cafe.adriel.voyager.navigator.currentOrThrow
import com.wellnesswingman.ui.screens.nutrition.NutritionalProfilesScreen
import com.wellnesswingman.ui.screens.googleexport.GoogleDocsExportScreen

class SettingsScreen : Screen {
    @Composable
    override fun Content() {
        val navigator = LocalNavigator.currentOrThrow

        Scaffold(
            topBar = {
                TopAppBar(
                    title = { Text("Settings") },
                    navigationIcon = {
                        IconButton(onClick = { navigator.pop() }) {
                            Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                        }
                    }
                )
            }
        ) { paddingValues ->
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(paddingValues)
            ) {
                SettingsMenuItem(
                    icon = Icons.Default.Person,
                    title = "User Profile",
                    subtitle = "Height, weight, DOB, activity level",
                    onClick = { navigator.push(UserProfileSettingsScreen()) }
                )
                HorizontalDivider()
                SettingsMenuItem(
                    icon = Icons.Default.FavoriteBorder,
                    title = "Share health diary",
                    subtitle = "Create a selective Google Doc for a clinician",
                    onClick = { navigator.push(GoogleDocsExportScreen()) }
                )
                HorizontalDivider()
                SettingsMenuItem(
                    icon = Icons.Default.Settings,
                    title = "LLM Provider",
                    subtitle = "Provider selection, API keys, models",
                    onClick = { navigator.push(LlmProviderSettingsScreen()) }
                )
                HorizontalDivider()
                SettingsMenuItem(
                    icon = Icons.Default.Notifications,
                    title = "Daily Check-ins",
                    subtitle = "Morning and evening prompts for how you feel",
                    onClick = { navigator.push(CheckInSettingsScreen()) }
                )
                HorizontalDivider()
                SettingsMenuItem(
                    icon = Icons.Default.FavoriteBorder,
                    title = "Polar Integration",
                    subtitle = "Connect your Polar fitness account",
                    onClick = { navigator.push(PolarSettingsScreen()) }
                )
                HorizontalDivider()
                SettingsMenuItem(
                    icon = Icons.Default.Restaurant,
                    title = "Nutrition Profiles",
                    subtitle = "Scan labels and manage exact packaged-food macros",
                    onClick = { navigator.push(NutritionalProfilesScreen()) }
                )
                HorizontalDivider()
                SettingsMenuItem(
                    icon = Icons.Default.Info,
                    title = "Diagnostics",
                    subtitle = "Share diagnostic logs",
                    onClick = { navigator.push(DiagnosticsSettingsScreen()) }
                )
                HorizontalDivider()
                SettingsMenuItem(
                    icon = Icons.Default.Build,
                    title = "Data Management",
                    subtitle = "Export and import data",
                    onClick = { navigator.push(DataManagementSettingsScreen()) }
                )
                HorizontalDivider()
            }
        }
    }
}

@Composable
private fun SettingsMenuItem(
    icon: ImageVector,
    title: String,
    subtitle: String,
    onClick: () -> Unit
) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 16.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(16.dp)
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.primary
        )
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = title,
                style = MaterialTheme.typography.bodyLarge
            )
            Text(
                text = subtitle,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        Icon(
            imageVector = Icons.AutoMirrored.Filled.ArrowForward,
            contentDescription = null,
            tint = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}
