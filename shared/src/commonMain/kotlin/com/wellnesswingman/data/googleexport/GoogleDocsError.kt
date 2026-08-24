package com.wellnesswingman.data.googleexport

/**
 * Sealed error hierarchy for Google Docs/Drive API failures.
 * Mirrors `PolarApiError` so callers get structured, taggable failures that
 * work directly with [Result.failure]. Bodies are kept because they help
 * diagnosis, but the client redacts tokens before logging.
 */
sealed class GoogleDocsError(message: String, cause: Throwable? = null) : Exception(message, cause) {
    /** Access token missing, expired, or rejected (401). */
    data class Unauthorized(val body: String = "") : GoogleDocsError("Google returned 401: $body")

    /** Rate limit exceeded (429) — caller should surface a retry hint. */
    data class RateLimited(val body: String = "") : GoogleDocsError("Google rate limited the request: $body")

    /** Server-side failure (5xx). */
    data class ServerError(val statusCode: Int, val body: String = "") :
        GoogleDocsError("Google server error $statusCode: $body")

    /** Network-level failure (offline, timeout, connection reset, DNS). */
    data class NetworkError(override val cause: Throwable) :
        GoogleDocsError("Google network error: ${cause.message}", cause)

    /** 2xx but the body was missing required fields or was unparseable. */
    data class InvalidResponse(val detail: String) : GoogleDocsError("Google invalid response: $detail")

    /** Delete target missing (404) — treated as already cleaned up. */
    data class NotFound(val detail: String = "") : GoogleDocsError("Google resource not found: $detail")

    /** 403 scope/consent denial surfaced by the API. */
    data class Forbidden(val body: String = "") : GoogleDocsError("Google refused access (403): $body")
}