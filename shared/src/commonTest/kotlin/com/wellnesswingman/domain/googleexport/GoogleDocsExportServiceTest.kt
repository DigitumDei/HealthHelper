package com.wellnesswingman.domain.googleexport

import com.wellnesswingman.data.googleexport.GoogleDocsApiClient
import com.wellnesswingman.data.googleexport.GoogleDocsBatchBuilder
import com.wellnesswingman.data.googleexport.GoogleDocsError
import com.wellnesswingman.domain.report.HealthReportBlock
import com.wellnesswingman.domain.report.HealthReportDocument
import io.ktor.client.HttpClient
import io.ktor.client.engine.mock.MockEngine
import io.ktor.client.engine.mock.MockRequestHandleScope
import io.ktor.client.engine.mock.respond
import io.ktor.client.plugins.contentnegotiation.ContentNegotiation
import io.ktor.client.request.HttpRequestData
import io.ktor.client.request.HttpResponseData
import io.ktor.http.ContentType
import io.ktor.http.HttpHeaders
import io.ktor.http.HttpMethod
import io.ktor.http.HttpStatusCode
import io.ktor.http.headersOf
import io.ktor.serialization.kotlinx.json.json
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertIs
import kotlin.test.assertTrue

class GoogleDocsExportServiceTest {

    private val jsonHeaders = headersOf(HttpHeaders.ContentType, ContentType.Application.Json.toString())

    private fun createClient(
        handler: suspend MockRequestHandleScope.(HttpRequestData) -> HttpResponseData
    ): HttpClient = HttpClient(MockEngine(handler)) {
        install(ContentNegotiation) {
            json(Json { ignoreUnknownKeys = true })
        }
    }

    private fun service(client: HttpClient) = GoogleDocsExportService(
        apiClient = GoogleDocsApiClient(client),
        batchBuilder = GoogleDocsBatchBuilder()
    )

    private val document = HealthReportDocument(listOf(HealthReportBlock.Heading(1, "Report")))

    @Test
    fun `successful export returns the created document`() = runTest {
        val client = createClient { request ->
            when {
                request.method == HttpMethod.Post && request.url.encodedPath == "/v1/documents" ->
                    respond("""{"documentId":"doc-1"}""", HttpStatusCode.OK, jsonHeaders)
                else -> respond("", HttpStatusCode.OK)
            }
        }

        val result = service(client).createAndPopulate("token", "Report", document)

        assertTrue(result.isSuccess)
        val exported = result.getOrThrow()
        assertEquals("Report", exported.title)
        assertEquals("doc-1", exported.documentId)
        assertTrue(exported.url.contains("doc-1"))
    }

    @Test
    fun `cancellation during population deletes the created document and rethrows`() = runTest {
        var deleteCalls = 0
        val client = createClient { request ->
            when {
                request.method == HttpMethod.Post && request.url.encodedPath == "/v1/documents" ->
                    respond("""{"documentId":"doc-1"}""", HttpStatusCode.OK, jsonHeaders)
                request.method == HttpMethod.Delete -> {
                    deleteCalls += 1
                    respond("", HttpStatusCode.OK)
                }
                else -> throw CancellationException("cancelled during population")
            }
        }

        val result = runCatching { service(client).createAndPopulate("token", "Report", document) }

        assertIs<CancellationException>(result.exceptionOrNull())
        assertEquals(1, deleteCalls)
    }

    @Test
    fun `rate-limited population reports cleaned-up partial document`() = runTest {
        val client = createClient { request ->
            when {
                request.method == HttpMethod.Post && request.url.encodedPath == "/v1/documents" ->
                    respond("""{"documentId":"doc-1"}""", HttpStatusCode.OK, jsonHeaders)
                request.method == HttpMethod.Delete ->
                    respond("", HttpStatusCode.OK)
                else -> respond("""{"error":"rate limited"}""", HttpStatusCode.TooManyRequests, jsonHeaders)
            }
        }

        val result = service(client).createAndPopulate("token", "Report", document)

        assertTrue(result.isFailure)
        val failure = result.exceptionOrNull() as GoogleExportError.ApiFailure
        assertIs<GoogleDocsError.RateLimited>(failure.failure)
        assertEquals("doc-1", failure.partialDocumentId)
        assertTrue(failure.cleanupDeleted)
    }

    @Test
    fun `invalid Google request surfaces its validation message`() = runTest {
        val client = createClient { request ->
            when {
                request.method == HttpMethod.Post && request.url.encodedPath == "/v1/documents" ->
                    respond("""{"documentId":"doc-1"}""", HttpStatusCode.OK, jsonHeaders)
                request.method == HttpMethod.Delete ->
                    respond("", HttpStatusCode.OK)
                else -> respond(
                    """{"error":{"message":"Invalid requests[2].updateParagraphStyle field mask."}}""",
                    HttpStatusCode.BadRequest,
                    jsonHeaders
                )
            }
        }

        val result = service(client).createAndPopulate("token", "Report", document)

        val failure = result.exceptionOrNull() as GoogleExportError.ApiFailure
        assertIs<GoogleDocsError.InvalidResponse>(failure.failure)
        assertEquals(
            "Google rejected the export request: Invalid requests[2].updateParagraphStyle field mask.",
            failure.toUserMessage()
        )
    }

    @Test
    fun `failed cleanup reports the incomplete document`() = runTest {
        val client = createClient { request ->
            when {
                request.method == HttpMethod.Post && request.url.encodedPath == "/v1/documents" ->
                    respond("""{"documentId":"doc-1"}""", HttpStatusCode.OK, jsonHeaders)
                request.method == HttpMethod.Delete ->
                    respond("", HttpStatusCode.InternalServerError)
                else -> respond("""{"error":"rate limited"}""", HttpStatusCode.TooManyRequests, jsonHeaders)
            }
        }

        val result = service(client).createAndPopulate("token", "Report", document)

        assertTrue(result.isFailure)
        val failure = result.exceptionOrNull() as GoogleExportError.ApiFailure
        assertEquals("doc-1", failure.partialDocumentId)
        assertFalse(failure.cleanupDeleted)
    }

    @Test
    fun `cancelled delete during cleanup rethrows cancellation without a result`() = runTest {
        val client = createClient { request ->
            when {
                request.method == HttpMethod.Post && request.url.encodedPath == "/v1/documents" ->
                    respond("""{"documentId":"doc-1"}""", HttpStatusCode.OK, jsonHeaders)
                else -> throw CancellationException("cancelled during cleanup")
            }
        }

        val result = runCatching { service(client).createAndPopulate("token", "Report", document) }

        assertIs<CancellationException>(result.exceptionOrNull())
    }
}
