# Structured Output TODO

## Goal
Adopt JSON schema-based responses from the LLM so analysis data is structured, versioned, and easy to consume downstream.

## Benefits
- Reliable fields without brittle text parsing.
- Clear schema evolution & versioning.
- Simpler aggregation for summaries, dashboards, or exports.
- Easier validation & logging of provider regressions.

## Implementation Tasks
1. **Schema Definition**
   - Draft a `MealAnalysisResult` JSON schema (food items, nutrients, confidence, errors).
   - Version the schema (`schemaVersion`) and keep sample payloads for regression tests.

2. **LLM Invocation Changes**
   - Update `OpenAiLlmClient` to include `response_format`/`json_schema` with the new schema.
   - Validate structured responses; log and handle schema violations gracefully.

3. **Domain Model Updates**
   - Extend `EntryAnalysis` with `SchemaVersion` and keep `InsightsJson` storing raw structured JSON.
   - Add DTOs in `Models` or `Utilities` to deserialize the structured output.

4. **Persistence & UI Integration**
   - Ensure repositories persist the schema version.
   - Update view models/pages to deserialize and display structured data (e.g., food list, nutrients).

5. **Testing & Tooling**
   - Add unit tests using mocked `ILLmClient` responses that match the schema.
   - Consider a lightweight validator CLI/test to ensure new schema revisions stay backwards compatible.

## Risks & Considerations
- Model drift could still break schema compliance: keep telemetry/logs for failed parses.
- Larger prompts/responses may affect token usage; monitor `LlmDiagnostics` metrics.
- Plan a migration strategy for legacy free-form rows if they need to be reprocessed.

