using System;
using System.ClientModel;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using HealthHelper.Models;
using HealthHelper.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using OpenAI;
using OpenAI.Chat;

namespace HealthHelper.Services.Llm;

public class OpenAiLlmClient : ILLmClient
{
    private readonly ILogger<OpenAiLlmClient> _logger;

    public OpenAiLlmClient(ILogger<OpenAiLlmClient> logger)
    {
        _logger = logger;
    }

    public async Task<LlmAnalysisResult> InvokeAnalysisAsync(
        TrackedEntry entry,
        LlmRequestContext context,
        string? existingAnalysisJson = null,
        string? correction = null)
    {
        if (string.IsNullOrWhiteSpace(context.ApiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        var client = new OpenAIClient(context.ApiKey);
        var chatClient = client.GetChatClient(context.ModelId);

        var messages = await CreateUnifiedChatRequest(entry, existingAnalysisJson, correction).ConfigureAwait(false);

        try
        {
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await CompleteChatWithFallbackAsync(chatClient, messages, options).ConfigureAwait(false);

            var insights = ExtractTextContent(response.Value.Content);

            UnifiedAnalysisResult? parsedResult = null;
            try
            {
                parsedResult = JsonSerializer.Deserialize<UnifiedAnalysisResult>(insights);
                _logger.LogInformation(
                    "Parsed unified analysis for entry {EntryId} detected as {EntryType}.",
                    entry.EntryId,
                    parsedResult?.EntryType ?? "Unknown");
            }
            catch (JsonException jsonEx)
            {
                _logger.LogWarning(jsonEx, "Failed to parse unified analysis response. Storing raw JSON. Response: {Response}", insights);
            }

            var analysis = new EntryAnalysis
            {
                EntryId = entry.EntryId,
                ProviderId = context.Provider.ToString(),
                Model = context.ModelId,
                CapturedAt = DateTime.UtcNow,
                InsightsJson = insights,
                SchemaVersion = parsedResult?.SchemaVersion ?? "unknown"
            };

            var diagnostics = new LlmDiagnostics
            {
                PromptTokenCount = response.Value.Usage.InputTokenCount,
                CompletionTokenCount = response.Value.Usage.OutputTokenCount,
                TotalTokenCount = response.Value.Usage.TotalTokenCount,
            };

            return new LlmAnalysisResult
            {
                Analysis = analysis,
                Diagnostics = diagnostics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI unified analysis request failed.");
            throw;
        }
    }

    public async Task<LlmAnalysisResult> InvokeDailySummaryAsync(
        DailySummaryRequest summaryRequest,
        LlmRequestContext context,
        string? existingSummaryJson = null)
    {
        if (string.IsNullOrWhiteSpace(context.ApiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        var client = new OpenAIClient(context.ApiKey);
        var chatClient = client.GetChatClient(context.ModelId);

        var messages = CreateDailySummaryChatRequest(summaryRequest, existingSummaryJson);

        try
        {
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await CompleteChatWithFallbackAsync(chatClient, messages, options).ConfigureAwait(false);

            var insights = ExtractTextContent(response.Value.Content);

            DailySummaryResult? parsedResult = null;
            try
            {
                parsedResult = JsonSerializer.Deserialize<DailySummaryResult>(insights);
                _logger.LogInformation(
                    "Parsed structured daily summary for {SummaryDate} covering {MealCount} meals.",
                    summaryRequest.SummaryDate.ToString("yyyy-MM-dd"),
                    summaryRequest.Meals.Count);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogWarning(jsonEx, "Failed to parse structured daily summary response. Storing raw JSON. Response: {Response}", insights);
            }

            var analysis = new EntryAnalysis
            {
                EntryId = summaryRequest.SummaryEntryId,
                ProviderId = context.Provider.ToString(),
                Model = context.ModelId,
                CapturedAt = DateTime.UtcNow,
                InsightsJson = insights,
                SchemaVersion = parsedResult?.SchemaVersion ?? "unknown"
            };

            var diagnostics = new LlmDiagnostics
            {
                PromptTokenCount = response.Value.Usage.InputTokenCount,
                CompletionTokenCount = response.Value.Usage.OutputTokenCount,
                TotalTokenCount = response.Value.Usage.TotalTokenCount,
            };

            return new LlmAnalysisResult
            {
                Analysis = analysis,
                Diagnostics = diagnostics
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenAI daily summary request failed.");
            throw;
        }
    }

    private async Task<ClientResult<ChatCompletion>> CompleteChatWithFallbackAsync(
        ChatClient chatClient,
        IReadOnlyList<ChatMessage> messages,
        ChatCompletionOptions options)
    {
        try
        {
            return await chatClient.CompleteChatAsync(messages, options).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex) when (IsResponseFormatSerializationBug(ex))
        {
            _logger.LogWarning(ex, "Response format serialization failed; retrying without explicit format.");
            return await chatClient.CompleteChatAsync(messages).ConfigureAwait(false);
        }
    }

    private static bool IsResponseFormatSerializationBug(InvalidOperationException ex)
    {
        return ex.Message?.Contains("WriteCore method", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static async Task<List<ChatMessage>> CreateUnifiedChatRequest(
        TrackedEntry entry,
        string? existingAnalysisJson,
        string? correction)
    {
        var systemPrompt = $@"You are a helpful assistant that analyzes images to track health and wellness.

First, determine the entry type based on the image contents:
- ""Meal"": Food, beverages, nutrition labels, meal prep scenes.
- ""Exercise"": Workout screenshots or photos showing fitness data (runs, rides, gym tracking, heart rate charts).
- ""Sleep"": Sleep tracking screenshots, bedroom environments, beds indicating rest.
- ""Other"": Anything else that doesn't fit the categories above.

Then provide a detailed analysis for the detected type:
- Meals: identify foods, estimate portions and nutrition, note health insights and recommendations.
- Exercise: extract displayed metrics (distance, duration, pace, calories, heart rate, etc.) and offer performance feedback.
- Sleep: summarise sleep duration/quality metrics, environment observations, improvement tips.
- Other: briefly describe the content and provide any helpful observations.

Return JSON that exactly matches this schema:
{GetUnifiedAnalysisSchema()}

Important rules:
- Only populate the analysis object that matches the detected entryType; set the others to null.
- Always include a confidence score between 0.0 and 1.0 reflecting how certain you are about the classification.
- Include warnings when information is missing, unclear, or potentially incorrect.
- If the user provides a correction, incorporate it and regenerate the full JSON.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt)
        };

        var userContent = new List<ChatMessageContentPart>
        {
            ChatMessageContentPart.CreateTextPart("Analyze this image and return the unified JSON response.")
        };

        if (!string.IsNullOrWhiteSpace(entry.BlobPath))
        {
            var absolutePath = Path.Combine(FileSystem.AppDataDirectory, entry.BlobPath);
            if (File.Exists(absolutePath))
            {
                var mimeType = Path.GetExtension(entry.BlobPath).ToLowerInvariant() switch
                {
                    ".jpg" => "image/jpeg",
                    ".jpeg" => "image/jpeg",
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    _ => "image/jpeg"
                };

                var imageBytes = await File.ReadAllBytesAsync(absolutePath).ConfigureAwait(false);
                userContent.Add(ChatMessageContentPart.CreateImagePart(new BinaryData(imageBytes), mimeType));
            }
        }

        messages.Add(new UserChatMessage(userContent));

        if (!string.IsNullOrWhiteSpace(existingAnalysisJson))
        {
            messages.Add(new AssistantChatMessage(existingAnalysisJson));
            messages.Add(new UserChatMessage("The previous message is the earlier JSON response. Update it to reflect the latest instructions."));
        }

        if (!string.IsNullOrWhiteSpace(correction))
        {
            messages.Add(new UserChatMessage($"User correction:\n{correction.Trim()}"));
        }

        return messages;
    }

    private static string ExtractTextContent(IReadOnlyList<ChatMessageContentPart> contentParts)
    {
        if (contentParts is null || contentParts.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (var part in contentParts)
        {
            if (part.Kind == ChatMessageContentPartKind.Text && !string.IsNullOrWhiteSpace(part.Text))
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(part.Text);
            }
        }

        return builder.ToString();
    }

    private static List<ChatMessage> CreateDailySummaryChatRequest(
        DailySummaryRequest summaryRequest,
        string? existingSummaryJson)
    {
        var systemPrompt = $@"You are a helpful nutrition coach generating a daily summary from prior meal analyses.
Use the provided structured meal data to calculate totals, evaluate nutritional balance, and offer insights.
Do not request or expect meal images – only use the supplied analysis data.

You MUST return a JSON object matching this exact schema:
{GetDailySummarySchema()}

Important rules:
- Always include every required property from the schema
- Provide empty arrays when there are no insights or recommendations
- Use null for unknown numeric values
- Ensure schemaVersion is ""1.0""
- Reference meals by their entryId values when describing insights
- If no meals are available, still return a valid JSON object with empty collections and explanations
";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt)
        };

        var builder = new StringBuilder();
        builder.AppendLine($"SummaryDate: {summaryRequest.SummaryDate:yyyy-MM-dd}");

        if (!string.IsNullOrWhiteSpace(summaryRequest.SummaryTimeZoneId) || summaryRequest.SummaryUtcOffsetMinutes is not null)
        {
            var offsetText = summaryRequest.SummaryUtcOffsetMinutes is int offset
                ? DateTimeConverter.FormatOffset(offset)
                : "unknown";
            builder.AppendLine($"SummaryTimeZone: {summaryRequest.SummaryTimeZoneId ?? "unknown"} (UTC{offsetText})");
        }

        builder.AppendLine($"MealsCaptured: {summaryRequest.Meals.Count}");
        builder.AppendLine("Meals:");

        foreach (var (meal, index) in summaryRequest.Meals.Select((m, i) => (m, i + 1)))
        {
            builder.AppendLine($"- Meal {index} (EntryId: {meal.EntryId})");
            builder.AppendLine($"  CapturedAtUtc: {meal.CapturedAt:O}");

            if (meal.CapturedAtLocal != default)
            {
                builder.AppendLine($"  CapturedAtLocal: {meal.CapturedAtLocal:O}");
            }
            else
            {
                builder.AppendLine("  CapturedAtLocal: unknown");
            }

            var mealOffsetText = meal.UtcOffsetMinutes is int mealOffset
                ? DateTimeConverter.FormatOffset(mealOffset)
                : "unknown";
            builder.AppendLine($"  TimeZone: {meal.TimeZoneId ?? "unknown"} (UTC{mealOffsetText})");
            if (!string.IsNullOrWhiteSpace(meal.Description))
            {
                builder.AppendLine($"  Description: {meal.Description}");
            }

            if (meal.Analysis is not null)
            {
                var json = JsonSerializer.Serialize(meal.Analysis);
                builder.AppendLine("  MealAnalysisJson: ");
                builder.AppendLine(json);
            }
            else
            {
                builder.AppendLine("  MealAnalysisJson: null");
            }

            builder.AppendLine();
        }

        messages.Add(new UserChatMessage(builder.ToString()));

        if (!string.IsNullOrWhiteSpace(existingSummaryJson))
        {
            messages.Add(new AssistantChatMessage(existingSummaryJson));
            messages.Add(new UserChatMessage("Regenerate the complete daily summary JSON considering any new data."));
        }

        return messages;
    }

    private static string GetUnifiedAnalysisSchema()
    {
        return """
        {
          "schemaVersion": "1.0",
          "entryType": "Meal",
          "confidence": 0.87,
          "mealAnalysis": {
            "schemaVersion": "1.0",
            "foodItems": [
              {
                "name": "grilled salmon",
                "portionSize": "180g",
                "calories": 360,
                "confidence": 0.92
              },
              {
                "name": "steamed broccoli",
                "portionSize": "1 cup",
                "calories": 55,
                "confidence": 0.88
              }
            ],
            "nutrition": {
              "totalCalories": 540,
              "protein": 42,
              "carbohydrates": 18,
              "fat": 28,
              "fiber": 6,
              "sugar": 4,
              "sodium": 510
            },
            "healthInsights": {
              "healthScore": 8.2,
              "summary": "Balanced meal with lean protein and vegetables.",
              "positives": [
                "High in protein",
                "Includes cruciferous vegetables"
              ],
              "improvements": [
                "Add a complex carbohydrate for sustained energy"
              ],
              "recommendations": [
                "Consider adding brown rice or quinoa on the side"
              ]
            },
            "confidence": 0.87,
            "warnings": []
          },
          "exerciseAnalysis": null,
          "sleepAnalysis": null,
          "otherAnalysis": null,
          "warnings": []
        }
        """;
    }

    private static string GetDailySummarySchema()
    {
        return """
        {
          "type": "object",
          "properties": {
            "schemaVersion": {
              "type": "string",
              "description": "Schema version, always '1.0'"
            },
            "totals": {
              "type": "object",
              "properties": {
                "calories": { "type": ["number", "null"], "description": "Total calories for the day" },
                "protein": { "type": ["number", "null"], "description": "Total protein (g)" },
                "carbohydrates": { "type": ["number", "null"], "description": "Total carbohydrates (g)" },
                "fat": { "type": ["number", "null"], "description": "Total fat (g)" },
                "fiber": { "type": ["number", "null"], "description": "Total fiber (g)" },
                "sugar": { "type": ["number", "null"], "description": "Total sugar (g)" },
                "sodium": { "type": ["number", "null"], "description": "Total sodium (mg)" }
              },
              "required": ["calories", "protein", "carbohydrates", "fat", "fiber", "sugar", "sodium"],
              "additionalProperties": false
            },
            "balance": {
              "type": "object",
              "properties": {
                "overall": { "type": ["string", "null"], "description": "Overall nutritional balance assessment" },
                "macroBalance": { "type": ["string", "null"], "description": "Macro nutrient balance observations" },
                "timing": { "type": ["string", "null"], "description": "Meal timing observations" },
                "variety": { "type": ["string", "null"], "description": "Variety and diversity assessment" }
              },
              "required": ["overall", "macroBalance", "timing", "variety"],
              "additionalProperties": false
            },
            "insights": {
              "type": "array",
              "items": { "type": "string" },
              "description": "Key insights about the day's nutrition"
            },
            "recommendations": {
              "type": "array",
              "items": { "type": "string" },
              "description": "Actionable recommendations for future meals"
            },
            "mealsIncluded": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "entryId": { "type": "integer", "description": "TrackedEntry identifier" },
                  "capturedAt": { "type": "string", "format": "date-time", "description": "Capture timestamp in ISO 8601" },
                  "summary": { "type": ["string", "null"], "description": "Short note about the meal" }
                },
                "required": ["entryId", "capturedAt", "summary"],
                "additionalProperties": false
              },
              "description": "Meals represented in the summary"
            }
          },
          "required": ["schemaVersion", "totals", "balance", "insights", "recommendations", "mealsIncluded"],
          "additionalProperties": false
        }
        """;
    }
}
