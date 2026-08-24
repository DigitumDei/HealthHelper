package com.wellnesswingman.platform

import android.content.Context
import android.content.Intent
import android.content.IntentSender
import com.google.android.gms.auth.api.identity.AuthorizationRequest
import com.google.android.gms.auth.api.identity.Identity
import com.google.android.gms.common.api.CommonStatusCodes
import com.google.android.gms.common.api.Scope
import com.wellnesswingman.domain.googleexport.GoogleAuthResult
import com.wellnesswingman.domain.googleexport.GoogleAuthService
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.suspendCancellableCoroutine
import io.github.aakira.napier.Napier
import kotlin.coroutines.resume

/**
 * Android [GoogleAuthService] built on the Google Play services
 * `AuthorizationClient` API — the non-deprecated successor to
 * `SignInWithGoogle.requestScopes` that requests just-in-time OAuth scopes.
 *
 * On-device flow:
 *  1. [requestAccess] calls `authorize()` scoped to `drive.file`. If the
 *     account has already granted the scope, a fresh access token is returned
 *     with no UI (ConsentRequired never appears).
 *  2. Otherwise the result carries a `PendingIntent` for the consent screen,
 *     which is exposed via [consentIntent] for the hosting Activity to launch.
 *     After the screen completes, [deliverConsentResult] records the outcome.
 *  3. A subsequent [requestAccess] returns the completed outcome (Granted with
 *     the in-memory token, Cancelled, or Denied).
 *
 * Security posture: the access token is held in memory for the session and
 * cleared by the caller once the export finishes. No refresh token is ever
 * requested, nothing is persisted, and no broker is involved. Account
 * selection and switching are provided by the Google consent screen itself.
 */
class AndroidGoogleAuthService(
    context: Context
) : GoogleAuthService {

    private val authorizationClient = Identity.getAuthorizationClient(context)

    private val _consentIntent = MutableStateFlow<IntentSender?>(null)
    val consentIntent: StateFlow<IntentSender?> = _consentIntent.asStateFlow()

    /** In-memory token valid for the current session's export only. */
    @Volatile
    private var inMemoryAccessToken: String? = null

    /** Outcome of a completed consent screen, awaiting the next [requestAccess]. */
    @Volatile
    private var deliveredResult: GoogleAuthResult? = null

    override val isAvailable: Boolean get() = true

    override suspend fun requestAccess(): GoogleAuthResult {
        inMemoryAccessToken?.let { return GoogleAuthResult.Granted(it) }

        deliveredResult?.let { result ->
            deliveredResult = null
            if (result is GoogleAuthResult.Granted) inMemoryAccessToken = result.accessToken
            return result
        }

        return suspendCancellableCoroutine { continuation ->
            val task = authorizationClient.authorize(buildRequest())
            task.addOnSuccessListener { resultBlocks ->
                if (resultBlocks.hasResolution()) {
                    _consentIntent.value = resultBlocks.pendingIntent?.intentSender
                    continuation.resume(GoogleAuthResult.ConsentRequired)
                } else {
                    val token = resultBlocks.accessToken
                    if (token.isNullOrBlank()) {
                        Napier.w("Google authorization returned no access token")
                        continuation.resume(GoogleAuthResult.Denied)
                    } else {
                        inMemoryAccessToken = token
                        continuation.resume(GoogleAuthResult.Granted(token))
                    }
                }
            }
            task.addOnFailureListener { e ->
                val mapped = mapFailure(e)
                if (mapped != GoogleAuthResult.ConsentRequired) {
                    Napier.w("Google authorization failed (status redacted): $mapped")
                }
                continuation.resume(mapped)
            }
        }
    }

    /** Launches the stored consent screen (hosted by the Activity). */
    fun consumeConsentIntent(): IntentSender? {
        val sender = _consentIntent.value
        _consentIntent.value = null
        return sender
    }

    override fun deliverConsentResult(intentData: Any?) {
        val intent = intentData as? Intent
        if (intent == null) {
            // Backing out of the consent screen yields a null result intent.
            // Record the cancellation so the next requestAccess surfaces it
            // instead of reopening the consent flow.
            deliveredResult = GoogleAuthResult.Cancelled
            return
        }
        try {
            val result = authorizationClient.getAuthorizationResultFromIntent(intent)
            val token = result.accessToken
            if (token.isNullOrBlank()) {
                deliveredResult = GoogleAuthResult.Denied
            } else {
                inMemoryAccessToken = token
                deliveredResult = GoogleAuthResult.Granted(token)
            }
        } catch (t: Throwable) {
            val mapped = mapFailure(t).takeIf { it != GoogleAuthResult.ConsentRequired }
                ?: GoogleAuthResult.Denied
            deliveredResult = mapped
        }
    }

    /** Discards the in-memory token once the export is finished. */
    fun clearInMemoryToken() {
        inMemoryAccessToken = null
    }

    override fun clearAccessToken() {
        clearInMemoryToken()
    }

    private fun buildRequest(): AuthorizationRequest = AuthorizationRequest.builder()
        .setRequestedScopes(listOf(Scope("https://www.googleapis.com/auth/drive.file")))
        .build()

    private fun mapFailure(t: Throwable): GoogleAuthResult {
        val statusCode = (t as? com.google.android.gms.common.api.ApiException)
            ?.statusCode
        return when (statusCode) {
            CommonStatusCodes.CANCELED -> GoogleAuthResult.Cancelled
            null -> GoogleAuthResult.Failed
            else -> GoogleAuthResult.Denied
        }
    }
}