package com.wellnesswingman.data.googleexport

import com.wellnesswingman.domain.report.HealthReportBlock
import com.wellnesswingman.domain.report.HealthReportDocument
import kotlinx.serialization.json.int
import kotlinx.serialization.json.jsonObject
import kotlinx.serialization.json.jsonPrimitive
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue

class GoogleDocsBatchBuilderTest {

    private val builder = GoogleDocsBatchBuilder()

    @Test
    fun `starts at Google Docs writable body index and offsets format ranges`() {
        val batch = builder.build(
            HealthReportDocument(listOf(HealthReportBlock.Heading(1, "Report")))
        )

        assertEquals("Report\n", batch.text)
        val insert = batch.requests.first()["insertText"]!!.jsonObject
        assertEquals(1, insert["location"]!!.jsonObject["index"]!!.jsonPrimitive.int)

        val update = batch.requests[1]["updateParagraphStyle"]!!.jsonObject
        assertEquals("namedStyleType", update["fields"]!!.jsonPrimitive.content)
        assertEquals("HEADING_1", update["paragraphStyle"]!!.jsonObject["namedStyleType"]!!.jsonPrimitive.content)
        assertTrue("headingId" !in update["paragraphStyle"]!!.jsonObject)

        val styleRange = update["range"]!!.jsonObject
        assertEquals(1, styleRange["startIndex"]!!.jsonPrimitive.int)
        assertEquals(8, styleRange["endIndex"]!!.jsonPrimitive.int)
    }

    @Test
    fun `uses Google Docs bullet request over contiguous bullet paragraphs`() {
        val batch = builder.build(
            HealthReportDocument(listOf(HealthReportBlock.BulletList(listOf("One", "Two"))))
        )

        assertEquals(2, batch.paragraphCount)
        val bullets = batch.requests.last()["createParagraphBullets"]!!.jsonObject
        val range = bullets["range"]!!.jsonObject
        assertEquals(1, range["startIndex"]!!.jsonPrimitive.int)
        assertEquals(9, range["endIndex"]!!.jsonPrimitive.int)
        assertEquals("BULLET_DISC_CIRCLE_SQUARE", bullets["bulletPreset"]!!.jsonPrimitive.content)
    }

    @Test
    fun `puts divider range and field mask on update request`() {
        val batch = builder.build(
            HealthReportDocument(listOf(HealthReportBlock.Divider))
        )

        val update = batch.requests[1]["updateParagraphStyle"]!!.jsonObject
        assertEquals("borderTop", update["fields"]!!.jsonPrimitive.content)
        assertEquals(1, update["range"]!!.jsonObject["startIndex"]!!.jsonPrimitive.int)
        assertEquals(2, update["range"]!!.jsonObject["endIndex"]!!.jsonPrimitive.int)

        val style = update["paragraphStyle"]!!.jsonObject
        assertTrue("fields" !in style)
        assertTrue("range" !in style)
        assertTrue("borders" !in style)
        assertTrue("borderTop" in style)

        val border = style["borderTop"]!!.jsonObject
        assertTrue("top" !in border)
        assertEquals(0.0, border["color"]!!.jsonObject["color"]!!.jsonObject["rgbColor"]!!.jsonObject["red"]!!.jsonPrimitive.content.toDouble())
        assertEquals("SOLID", border["dashStyle"]!!.jsonPrimitive.content)
        assertEquals("PT", border["padding"]!!.jsonObject["unit"]!!.jsonPrimitive.content)
    }
}
