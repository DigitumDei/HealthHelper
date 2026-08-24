package com.wellnesswingman.data.googleexport

import com.wellnesswingman.domain.report.HealthReportBlock
import com.wellnesswingman.domain.report.HealthReportDocument
import kotlinx.serialization.json.JsonObject
import kotlinx.serialization.json.buildJsonObject
import kotlinx.serialization.json.put

/**
 * Pure mapper from a [HealthReportDocument] to the bounded set of Google Docs
 * `batchUpdate` request payloads.
 *
 * Index model (important for correctness):
 *  - The client inserts the whole authored body in a SINGLE `insertText` at
 *    index 1. The body is a linear string of paragraphs, and every paragraph is
 *    terminated by exactly one `\n`, so every character boundary is known ahead
 *    of time and no `document.get` round-trip is needed.
 *  - A paragraph starting at index `s` with content length `len` occupies the
 *    range `[s, s + len + 1)` — its text plus its terminating newline. Every
 *    style/bullet request uses those exact ranges.
 *  - Because `insertText` is called at the very start of the blank body
 *    paragraph, no paragraph merge occurs and the authored ranges stay exact.
 *
 * Block mapping:
 *  - Heading          -> a paragraph styled with `namedStyleType = HEADING_n`.
 *  - Paragraph        -> a plain body paragraph (default style).
 *  - BulletList       -> contiguous `\n`-terminated paragraphs given a disc
 *    glyph by a single `createParagraphBullets` request over the whole run.
 *  - Table            -> a row per line, columns joined by " | "; the header
 *    line is bolded. (Semantically a table without `insertTable` index
 *    gymnastics, which would otherwise require a read-back layout call.)
 *  - Divider          -> an empty paragraph with a solid top border.
 *
 * The batch is deliberately bounded: one `insertText` plus a small constant
 * number of requests per element. Nothing sensitive is written to logs.
 */
class GoogleDocsBatchBuilder {

    /** The initial empty paragraph in a newly-created Doc begins at index 1. */
    private companion object {
        const val DOCUMENT_BODY_START = 1
    }

    data class PreparedBatch(
        /** The exact single-`insertText` body string to insert at index 1. */
        val text: String,
        /** The ordered list of request payloads for one `batchUpdate` call. */
        val requests: List<JsonObject>,
        /** Number of paragraphs authored (diagnostics). */
        val paragraphCount: Int
    )

    fun build(document: HealthReportDocument): PreparedBatch {
        val text = StringBuilder()
        val paragraphs = mutableListOf<PreparedParagraph>()

        for (block in document.blocks) {
            when (block) {
                is HealthReportBlock.Heading ->
                    paragraphs += pushParagraph(text, block.text, Style.Heading(block.level))

                is HealthReportBlock.Paragraph ->
                    paragraphs += pushParagraph(text, block.text, Style.Body)

                is HealthReportBlock.BulletList -> {
                    val start = paragraphs.size
                    block.items.forEach { item ->
                        paragraphs += pushParagraph(text, item, Style.Body)
                    }
                    for (i in start until paragraphs.size) {
                        paragraphs[i] = paragraphs[i].copy(run = BulletRun)
                    }
                }

                is HealthReportBlock.Table -> {
                    val header = pushParagraph(text, block.headers.joinToString(" | "), Style.Body)
                    paragraphs += header.copy(run = BoldRun)
                    block.rows.forEach { row ->
                        paragraphs += pushParagraph(text, row.joinToString(" | "), Style.Body)
                    }
                }

                HealthReportBlock.Divider ->
                    paragraphs += pushParagraph(text, "", Style.Divider)
            }
        }

        return assemble(text.toString(), paragraphs)
    }

    // --- Paragraph model ---

    private data class PreparedParagraph(
        val text: String,
        val startIndex: Int,
        val endIndex: Int,
        val style: Style,
        val run: Run = PlainRun
    )

    private sealed interface Style {
        data class Heading(val level: Int) : Style
        data object Body : Style
        data object Divider : Style
    }

    private sealed interface Run
    private data object PlainRun : Run
    private data object BoldRun : Run
    private data object BulletRun : Run

    private fun pushParagraph(
        body: StringBuilder,
        content: String,
        style: Style
    ): PreparedParagraph {
        val start = body.length
        body.append(content)
        body.append('\n')
        return PreparedParagraph(
            text = content,
            startIndex = start,
            endIndex = start + content.length + 1,
            style = style
        )
    }

    // --- JSON assembly ---

    private fun assemble(text: String, paragraphs: List<PreparedParagraph>): PreparedBatch {
        val requests = mutableListOf<JsonObject>()

        if (paragraphs.isNotEmpty()) {
            requests += buildJsonObject {
                put(
                    "insertText",
                    buildJsonObject {
                        put("location", buildJsonObject { put("index", DOCUMENT_BODY_START) })
                        put("text", text)
                    }
                )
            }
        }

        paragraphs.forEach { paragraph ->
            val start = paragraph.startIndex
            val end = paragraph.endIndex

            when (paragraph.style) {
                is Style.Heading -> {
                    val level = paragraph.style.level.coerceIn(1, 6)
                    requests += buildJsonObject {
                        put(
                            "updateParagraphStyle",
                            buildJsonObject {
                                put(
                                    "paragraphStyle",
                                    buildJsonObject { put("namedStyleType", "HEADING_$level") }
                                )
                                put("fields", "namedStyleType")
                                put("range", range(start, end))
                            }
                        )
                    }
                }

                Style.Divider -> {
                    requests += buildJsonObject {
                        put(
                            "updateParagraphStyle",
                            buildJsonObject {
                                put(
                                    "paragraphStyle",
                                    buildJsonObject {
                                        put(
                                            "borderTop",
                                            buildJsonObject {
                                                put(
                                                    "color",
                                                    buildJsonObject {
                                                        put("color", buildJsonObject {
                                                            put("rgbColor", buildJsonObject {
                                                                put("red", 0.0)
                                                                put("green", 0.0)
                                                                put("blue", 0.0)
                                                            })
                                                        })
                                                    }
                                                )
                                                put("dashStyle", "SOLID")
                                                put("width", buildJsonObject {
                                                    put("magnitude", 1.0)
                                                    put("unit", "PT")
                                                })
                                                put("padding", buildJsonObject {
                                                    put("magnitude", 1.0)
                                                    put("unit", "PT")
                                                })
                                            }
                                        )
                                    }
                                )
                                put("fields", "borderTop")
                                put("range", range(start, end))
                            }
                        )
                    }
                }

                Style.Body -> Unit
            }

            if (paragraph.run == BoldRun) {
                requests += buildJsonObject {
                    put(
                        "updateTextStyle",
                        buildJsonObject {
                            put("textStyle", buildJsonObject { put("bold", true) })
                            put("fields", "bold")
                            put("range", range(start, start + paragraph.text.length))
                        }
                    )
                }
            }
        }

        bulletGroups(paragraphs).forEach { (first, last) ->
            requests += buildJsonObject {
                put(
                    "createParagraphBullets",
                    buildJsonObject {
                        put("range", range(first.startIndex, last.endIndex))
                        put("bulletPreset", "BULLET_DISC_CIRCLE_SQUARE")
                    }
                )
            }
        }

        return PreparedBatch(
            text = text,
            requests = requests,
            paragraphCount = paragraphs.size
        )
    }

    /**
     * Groups contiguous [BulletRun] paragraphs so one `createParagraphBullets` covers each
     * uninterrupted run.
     */
    private fun bulletGroups(
        paragraphs: List<PreparedParagraph>
    ): List<Pair<PreparedParagraph, PreparedParagraph>> {
        val groups = mutableListOf<Pair<PreparedParagraph, PreparedParagraph>>()
        var runStart: PreparedParagraph? = null
        var runLast: PreparedParagraph? = null

        paragraphs.forEach { paragraph ->
            if (paragraph.run == BulletRun) {
                if (runStart == null) runStart = paragraph
                runLast = paragraph
            } else {
                flushGroup(runStart, runLast, groups)
                runStart = null
                runLast = null
            }
        }
        flushGroup(runStart, runLast, groups)
        return groups
    }

    private fun flushGroup(
        start: PreparedParagraph?,
        last: PreparedParagraph?,
        groups: MutableList<Pair<PreparedParagraph, PreparedParagraph>>
    ) {
        if (start != null && last != null) groups += start to last
    }

    private fun range(startIndex: Int, endIndex: Int): JsonObject = buildJsonObject {
        put("startIndex", startIndex + DOCUMENT_BODY_START)
        put("endIndex", endIndex + DOCUMENT_BODY_START)
    }
}
