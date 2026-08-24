package com.wellnesswingman.data.googleexport

import io.github.aakira.napier.Napier
import io.ktor.client.HttpClient
import io.ktor.client.call.body
import io.ktor.client.plugins.contentnegotiation.ContentNegotiation
import io.ktor.client.request.HttpRequestBuilder
import io.ktor.client.request.delete
import io.ktor.client.request.header
import io.ktor.client.request.post
import io.ktor.client.request.setBody
import io.ktor.client.statement.HttpResponse
import io.ktor.client.statement.bodyAsText
import io.ktor.http.ContentType
import io.ktor.http.HttpHeaders
import io.ktor.http.HttpStatusCode
import io.ktor.http.contentType
import io.ktor.http.isSuccess
import io.ktor.serialization.kotlinx.json.json
import kotlinx.coroutines.CancellationException
import kotlinx.serialization.SerialName
import kotlinx.serialization.Serializable
import kotlinx.serialization.json.Json
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.buildJsonArray
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlinx.serialization.json.put

/**
 * Minimal REST client for creating and populating a Google Doc and deleting an
 * app-created file.
 *
 * Scope discipline: every call carries the caller's in-memory access token;
 * the token is never stored, logged, or re-used outside the active export.
 * Logs intentionally exclude the token, the document body, and the document
 * URL. Endpoints:
 *  - POST docs.googleapis.com/v1/documents                 (create)
 *  - POST docs.googleapis.com/v1/documents/{id}:batchUpdate (populate)
 *  - DELETE www.googleapis.com/drive/v3/files/{id}         (cleanup, app-created only)
 */
class GoogleDocsApiClient(
    private val httpClient: HttpClient = createDefaultHttpClient()
) {

    companion object {
        private const val DOCS_BASE_URL = "https://docs.googleapis.com/v1/documents"
        private const val DRIVE_FILES_URL = "https://www.googleapis.com/drive/v3/files"
        private const val MAX_REQUESTS_PER_BATCH = 500
        private const val MAX_ERROR_DETAIL_LENGTH = 500

        fun createDefaultHttpClient(): HttpClient = HttpClient {
            install(ContentNegotiation) {
                json(Json { ignoreUnknownKeys = true })
            }
        }
    }

    /**
     * Creates a blank Google Doc with [title] and returns the new document id.
     */
    suspend fun createDocument(accessToken: String, title: String): Result<String> = try {
        val response = httpClient.post(DOCS_BASE_URL) {
            token(accessToken)
            jsonBody(buildJsonObject { put("title", title) }.toString())
        }
        if (response.status.isSuccess()) {
            val dto: CreateDocumentResponse = response.body()
            if (dto.documentId.isBlank()) {
                failure("createDocument response had no document id")
            } else {
                Result.success(dto.documentId)
            }
        } else {
            failure(response, "createDocument")
        }
    } catch (e: CancellationException) {
        throw e
    } catch (e: Exception) {
        Napier.e("Google Docs createDocument network error")
        Result.failure(GoogleDocsError.NetworkError(e))
    }

    /**
     * Applies the built [requests] to an app-created document.
     */
    suspend fun batchUpdate(
        accessToken: String,
        documentId: String,
        requests: List<JsonObject>
    ): Result<Unit> {
        if (requests.isEmpty()) return Result.success(Unit)
        return try {
            requests.chunked(MAX_REQUESTS_PER_BATCH).forEach { requestBatch ->
                val result = batchUpdateChunk(accessToken, documentId, requestBatch)
                if (result.isFailure) return result
            }
            Result.success(Unit)
        } catch (e: CancellationException) {
            throw e
        } catch (e: Exception) {
            Napier.e("Google Docs batchUpdate network error")
            Result.failure(GoogleDocsError.NetworkError(e))
        }
    }

    /**
     * Deletes an app-created file. 404 is reported as [GoogleDocsError.NotFound]
     * so callers can treat an already-missing document as cleaned up.
     */
    suspend fun deleteDocument(accessToken: String, fileId: String): Result<Unit> = try {
        val response = httpClient.delete("$DRIVE_FILES_URL/$fileId") {
            token(accessToken)
        }
        when {
            response.status.isSuccess() -> Result.success(Unit)
            response.status == HttpStatusCode.NotFound ->
                Result.failure(GoogleDocsError.NotFound("file already gone"))
            else -> failure(response, "deleteDocument")
        }
    } catch (e: CancellationException) {
        throw e
    } catch (e: Exception) {
        Napier.e("Google Drive deleteDocument network error")
        Result.failure(GoogleDocsError.NetworkError(e))
    }

    // --- Internals ---

    private fun HttpRequestBuilder.token(accessToken: String) {
        header(HttpHeaders.Authorization, "Bearer $accessToken")
    }

    private fun HttpRequestBuilder.jsonBody(body: String) {
        contentType(ContentType.Application.Json)
        setBody(body)
    }

    private suspend fun batchUpdateChunk(
        accessToken: String,
        documentId: String,
        requests: List<JsonObject>
    ): Result<Unit> {
        val body = buildJsonObject {
            put("requests", buildJsonArray { requests.forEach { add(it) } })
        }.toString()
        val response = httpClient.post("$DOCS_BASE_URL/$documentId:batchUpdate") {
            token(accessToken)
            jsonBody(body)
        }
        return if (response.status.isSuccess()) Result.success(Unit) else failure(response, "batchUpdate")
    }

    private suspend fun failure(
        response: HttpResponse,
        operation: String
    ): Result<Nothing> {
        val detail = response.googleErrorDetail()
        return when {
        response.status == HttpStatusCode.Unauthorized -> {
            Napier.w("Google Docs $operation returned 401")
            Result.failure(GoogleDocsError.Unauthorized())
        }
        response.status == HttpStatusCode.Forbidden -> {
            Napier.w("Google Docs $operation returned 403 (body redacted)")
            Result.failure(GoogleDocsError.Forbidden())
        }
        response.status == HttpStatusCode.TooManyRequests -> {
            Napier.w("Google Docs $operation returned 429")
            Result.failure(GoogleDocsError.RateLimited())
        }
        response.status.value in 500..599 -> {
            Napier.e("Google Docs $operation returned ${response.status.value}")
            Result.failure(GoogleDocsError.ServerError(response.status.value))
        }
        else -> {
            Napier.e("Google Docs $operation returned unexpected ${response.status.value}")
            Result.failure(GoogleDocsError.InvalidResponse(detail))
        }
        }
    }

    /**
     * Extracts the standard Google API validation message for local, user-facing
     * troubleshooting. Request bodies and access tokens are never logged.
     */
    private suspend fun HttpResponse.googleErrorDetail(): String {
        val message = runCatching {
            Json.parseToJsonElement(bodyAsText())
                .jsonObject["error"]
                ?.jsonObject
                ?.get("message")
                ?.jsonPrimitive
                ?.content
        }.getOrNull()
        return message
            ?.replace(Regex("\\s+"), " ")
            ?.take(MAX_ERROR_DETAIL_LENGTH)
            ?.takeIf { it.isNotBlank() }
            ?: "Google returned HTTP ${status.value}."
    }

    private fun failure(detail: String, operation: String = "request"): Result<Nothing> {
        Napier.e("Google Docs $operation failed: $detail")
        return Result.failure(GoogleDocsError.InvalidResponse(detail))
    }

    @Serializable
    private data class CreateDocumentResponse(
        @SerialName("documentId") val documentId: String
    )
}
