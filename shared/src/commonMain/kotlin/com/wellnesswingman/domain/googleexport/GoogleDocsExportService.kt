package com.wellnesswingman.domain.googleexport

import com.wellnesswingman.data.googleexport.GoogleDocsApiClient
import com.wellnesswingman.data.googleexport.GoogleDocsBatchBuilder
import com.wellnesswingman.data.googleexport.GoogleDocsError
import com.wellnesswingman.domain.report.HealthReportDocument
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.NonCancellable
import kotlinx.coroutines.withContext

/**
 * Creates, populates, and cleans up a Google Doc, mapping the underlying API
 * failures into [GoogleExportError].
 *
 * The access token is passed per-call by the caller (obtained just-in-time
 * from [GoogleAuthService]) and lives only in memory for the duration of this
 * operation. Nothing here logs the token, the document body, or the URL.
 */
class GoogleDocsExportService(
    private val apiClient: GoogleDocsApiClient,
    private val batchBuilder: GoogleDocsBatchBuilder
) {

    /**
     * Creates the document, applies the batch requests, and on a population
     * failure deletes the incomplete app-created document when possible.
     */
    suspend fun createAndPopulate(
        accessToken: String,
        title: String,
        document: HealthReportDocument,
        onDocumentCreated: (String) -> Unit = {}
    ): Result<GoogleDocsExportResult> {
        val createResult = apiClient.createDocument(accessToken, title)
        val documentId = createResult.getOrElse { failure ->
            return Result.failure(failure.toExportError())
        }
        onDocumentCreated(documentId)

        val batch = batchBuilder.build(document)
        val populateResult = try {
            apiClient.batchUpdate(accessToken, documentId, batch.requests)
        } catch (e: CancellationException) {
            // The document already exists, so cancellation would otherwise
            // orphan an empty app-created Doc. Attempt best-effort cleanup
            // outside the cancelled context before propagating.
            withContext(NonCancellable) {
                runCatching { deletePartialDocument(accessToken, documentId) }
            }
            throw e
        }
        if (populateResult.isSuccess) {
            return Result.success(
                GoogleDocsExportResult(
                    title = title,
                    documentId = documentId,
                    url = GoogleDocsExportResult.urlOf(documentId)
                )
            )
        }

        val rawFailure = populateResult.exceptionOrNull()
        val failure: GoogleDocsError = rawFailure as? GoogleDocsError
            ?: GoogleDocsError.InvalidResponse(rawFailure?.message ?: "unknown Google error")
        return Result.failure(cleanupAfterFailure(accessToken, documentId, failure))
    }

    /**
     * Deletes an app-created document (used for explicit cleanup and retries).
     * A 404 is treated as already-clean.
     */
    suspend fun deletePartialDocument(accessToken: String, documentId: String): Boolean {
        return apiClient.deleteDocument(accessToken, documentId)
            .fold(
                onSuccess = { true },
                onFailure = { failure ->
                    if (failure is GoogleDocsError.NotFound) {
                        true
                    } else {
                        false
                    }
                }
            )
    }

    private suspend fun cleanupAfterFailure(
        accessToken: String,
        documentId: String,
        cause: GoogleDocsError
    ): GoogleExportError {
        val deleted = try {
            deletePartialDocument(accessToken, documentId)
        } catch (e: CancellationException) {
            throw e
        } catch (_: Exception) {
            false
        }
        return GoogleExportError.ApiFailure(
            failure = cause,
            partialDocumentId = documentId,
            cleanupDeleted = deleted
        )
    }

    private fun Throwable.toExportError(): GoogleExportError {
        val docs = this as? GoogleDocsError
            ?: GoogleDocsError.InvalidResponse(message ?: "unknown Google error")
        return GoogleExportError.ApiFailure(docs)
    }
}