package com.wellnesswingman.platform

import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.IntentSenderRequest
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import com.wellnesswingman.domain.googleexport.GoogleAuthService
import org.koin.compose.getKoin

/**
 * Android [GoogleConsentHost]: watches [AndroidGoogleAuthService.consentIntent]
 * and launches the pending consent screen through the Activity result API, then
 * hands the result back to the auth service and notifies the exporter.
 */
@Composable
actual fun GoogleConsentHost(onCompleted: () -> Unit) {
    val koin = getKoin()
    val service = remember(koin) {
        runCatching { koin.get<GoogleAuthService>() }
            .getOrNull() as? AndroidGoogleAuthService
    }
    val launcher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.StartIntentSenderForResult()
    ) { result ->
        service?.deliverConsentResult(result.data)
        onCompleted()
    }
    LaunchedEffect(service) {
        val auth = service ?: return@LaunchedEffect
        auth.consentIntent.collect { sender ->
            if (sender != null) {
                auth.consumeConsentIntent()
                launcher.launch(IntentSenderRequest.Builder(sender).build())
            }
        }
    }
}
