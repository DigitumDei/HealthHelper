package com.wellnesswingman.domain.googleexport

import com.wellnesswingman.data.googleexport.GoogleDocsError

/**
 * Just-in-time result of requesting a Google access token for `drive.file`.
 *
 * Tokens are held in memory for the duration of one export only and are never
 * persisted by the app (no refresh tokens, no access-token storage, no broker).
 */
sealed interface GoogleAuthResult {

    /** The account has already granted `drive.file` and a fresh access token is available. */
    data class Granted(val accessToken: String) : GoogleAuthResult

    /**
     * The user must confirm the grant in the Google system consent screen.
     * The platform is responsible for launching it; the caller should continue
     * after the consent screen completes (success, denial, or cancellation).
     */
    data object ConsentRequired : GoogleAuthResult

    /** The user cancelled the consent flow (back press / dismissed). */
    data object Cancelled : GoogleAuthResult

    /** The user denied the requested scopes. */
    data object Denied : GoogleAuthResult

    /**
     * Authorization failed for a local/transient reason (for example a Play
     * services hiccup) that is not a scope denial and not a platform-support
     * problem. The user may retry; details are deliberately not surfaced.
     */
    data object Failed : GoogleAuthResult

    /** Authorization is not possible on this platform / Google Play services unavailable. */
    data object Unavailable : GoogleAuthResult
}

/**
 * Platform-neutral contract for requesting a one-time Google access token.
 *
 * Android implements this with the Google Play services `AuthorizationClient`
 * (the non-deprecated successor to `SignInWithGoogle.requestScopes`); desktop
 * returns [GoogleAuthResult.Unavailable] because Google Docs export is
 * Android-only for now. The token is in-memory only and dropped as soon as the
 * export operation finishes.
 */
interface GoogleAuthService {
    /** True when authorization could run on this platform (informational for UI state). */
    val isAvailable: Boolean

    /**
     * Requests a fresh access token for the minimal `https://www.googleapis.com/auth/drive.file`
     * scope. Returns [GoogleAuthResult.ConsentRequired] when a consent screen must be shown;
     * the platform surfaces that screen and the next call completes the flow.
     */
    suspend fun requestAccess(): GoogleAuthResult

    /**
     * Called by the Activity hosting the consent screen once its result Intent
     * is available. Android-only plumbing; no-op elsewhere.
     */
    fun deliverConsentResult(intentData: Any?)

    /**
     * Discards any in-memory access token after an export finishes so nothing
     * survives the session. No-op when no token is held or on platforms that
     * never obtain one.
     */
    fun clearAccessToken()
}

/**
 * Outcome of populating a Google Doc.
 */
@Suppress("DataClassShouldBeImmutable")
data class GoogleDocsExportResult(
    val title: String,
    val documentId: String,
    val url: String
) {
    companion object {
        fun urlOf(documentId: String): String = "https://docs.google.com/document/d/$documentId/edit"
    }
}

/**
 * Error carrying the reason and, when applicable, whether a partially-populated
 * app-created document exists so the UI can surface it clearly.
 */
sealed class GoogleExportError(
    message: String,
    cause: Throwable? = null
) : Exception(message, cause) {

    /** No data was selected/gathered, so authorization and upload are skipped. */
    data object EmptySelection : GoogleExportError("Nothing was selected to export")

    /** Authorization is not available on this platform. */
    data object UnsupportedPlatform : GoogleExportError("Exporting to Google Docs is not supported on this platform")

    /** The user cancelled the consent screen. */
    data object Cancelled : GoogleExportError("Google authorization was cancelled")

    /** The user denied the requested scopes. */
    data object Denied : GoogleExportError("Google authorization was denied")

    /** Network-level failure reaching Google (offline, timeout, DNS). */
    data class Offline(override val cause: Throwable) : GoogleExportError("No network connection while reaching Google", cause)

    /** A transient server-side failure (rate limit, 5xx) that the user may retry. */
    data class Transient(val detail: String) : GoogleExportError("Google is temporarily unavailable: $detail")

    /** The request was refused (expired/invalid token, 401/403). */
    data class NotAuthorized(val detail: String) : GoogleExportError("Google rejected the authorization: $detail")

    /** The server returned an unexpected response. */
    data class InvalidResponse(val detail: String) : GoogleExportError("Google returned an unexpected response: $detail")

    /**
     * A categorized failure that wraps an underlying [GoogleDocsError]. When a
     * partially-populated app-created document exists, [partialDocumentId] and
     * [cleanupDeleted] tell the UI whether it was removed or must be surfaced.
     */
    data class ApiFailure(
        val failure: GoogleDocsError,
        val partialDocumentId: String? = null,
        val cleanupDeleted: Boolean = false
    ) : GoogleExportError(
        when (failure) {
            is GoogleDocsError.RateLimited -> "Google is rate limiting requests, please wait and retry"
            is GoogleDocsError.ServerError -> "Google failed with a server error, please retry"
            is GoogleDocsError.Unauthorized -> "Google rejected the access token, please re-authorize"
            is GoogleDocsError.NetworkError -> "No network connection while reaching Google"
            is GoogleDocsError.InvalidResponse -> "Google rejected the export request: ${failure.detail}"
            is GoogleDocsError.NotFound -> "The Google document no longer exists"
            is GoogleDocsError.Forbidden -> "Google refused access, please re-authorize"
        },
        failure
    )

    fun toUserMessage(): String =
        message ?: "Google Docs export failed"
}
