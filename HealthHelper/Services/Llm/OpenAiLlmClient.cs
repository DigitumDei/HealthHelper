using System;
using System.ClientModel;
using System.Text;
using System.Text.Json;
using HealthHelper.Models;
using HealthHelper.Utilities;
using Microsoft.Extensions.Logging;
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

        var messages = await CreateChatRequest(entry, existingAnalysisJson, correction);

        try
        {
            // Note: CreateJsonSchemaFormat causes serialization errors on Android
            // Using CreateJsonObjectFormat as first pass - schema is still enforced via prompt
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonObjectFormat()
            };

            var response = await CompleteChatWithFallbackAsync(chatClient, messages, options);

            var insights = ExtractTextContent(response.Value.Content);

            // Validate and parse the structured response
            MealAnalysisResult? parsedResult = null;
            try
            {
                parsedResult = JsonSerializer.Deserialize<MealAnalysisResult>(insights);
                _logger.LogInformation("Successfully parsed structured meal analysis with {FoodItemCount} food items.",
                    parsedResult?.FoodItems?.Count ?? 0);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogWarning(jsonEx, "Failed to parse structured response, storing raw JSON. Response: {Response}", insights);
            }

            var analysis = new EntryAnalysis
            {
                EntryId = entry.EntryId,
                ProviderId = LlmProvider.OpenAI.ToString(),
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
            _logger.LogError(ex, "OpenAI API request failed.");
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

    private async Task<ClientResult<ChatCompletion>> CompleteChatWithFallbackAsync(ChatClient chatClient, IReadOnlyList<ChatMessage> messages, ChatCompletionOptions options)
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

    private static async Task<List<ChatMessage>> CreateChatRequest(
        TrackedEntry entry,
        string? existingAnalysisJson,
        string? correction)
    {
        var systemPrompt = $@"You are a helpful assistant that analyzes meal photos.
Identify all food items, estimate portion sizes, and provide nutritional information.
Give an overall health assessment with specific recommendations.
Be accurate but acknowledge uncertainty when applicable.

If the user provides corrections or additional details after your previous response,
incorporate the new information and regenerate the entire JSON output.

You MUST return your analysis as a JSON object matching this exact schema:
{GetMealAnalysisSchema()}

Important rules:
- If no food is detected, return an empty foodItems array and add a warning explaining why
- All required fields must be present, use null for unknown values
- Arrays can be empty but must be present
- Confidence values must be between 0.0 and 1.0
- schemaVersion must always be ""1.0""";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
        };

        var userInstruction = string.IsNullOrWhiteSpace(correction)
            ? "Analyze this meal."
            : "Re-analyze this meal using the user's corrections.";

        var userMessageContent = new List<ChatMessageContentPart>
        {
            ChatMessageContentPart.CreateTextPart(userInstruction)
        };

        var absoluteBlobPath = Path.Combine(FileSystem.AppDataDirectory, entry.BlobPath ?? string.Empty);
        if (!string.IsNullOrEmpty(entry.BlobPath))
        {
            var mimeType = Path.GetExtension(entry.BlobPath).ToLowerInvariant() switch
            {
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                _ => "image/jpeg"
            };

            if (File.Exists(absoluteBlobPath))
            {
                var imageBytes = await File.ReadAllBytesAsync(absoluteBlobPath).ConfigureAwait(false);
                userMessageContent.Add(ChatMessageContentPart.CreateImagePart(new BinaryData(imageBytes), mimeType));
            }
        }

        messages.Add(new UserChatMessage(userMessageContent));

        if (!string.IsNullOrWhiteSpace(existingAnalysisJson))
        {
            messages.Add(new AssistantChatMessage(existingAnalysisJson));
        }

        if (!string.IsNullOrWhiteSpace(correction))
        {
            var correctionText = correction.Trim();
            var correctionParts = new List<ChatMessageContentPart>
            {
                ChatMessageContentPart.CreateTextPart($"Correction from the user:\n{correctionText}")
            };

            messages.Add(new UserChatMessage(correctionParts));
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

    private static string GetMealAnalysisSchema()
    {
        return """
        {
          "type": "object",
          "properties": {
            "schemaVersion": {
              "type": "string",
              "description": "Schema version, always '1.0'"
            },
            "foodItems": {
              "type": "array",
              "description": "List of detected food items",
              "items": {
                "type": "object",
                "properties": {
                  "name": {
                    "type": "string",
                    "description": "Name of the food item"
                  },
                  "portionSize": {
                    "type": ["string", "null"],
                    "description": "Estimated portion size (e.g., '1 cup', '150g')"
                  },
                  "calories": {
                    "type": ["integer", "null"],
                    "description": "Estimated calories for this item"
                  },
                  "confidence": {
                    "type": "number",
                    "description": "Confidence in detection (0.0 to 1.0)"
                  }
                },
                "required": ["name", "portionSize", "calories", "confidence"],
                "additionalProperties": false
              }
            },
            "nutrition": {
              "type": ["object", "null"],
              "description": "Estimated nutritional information",
              "properties": {
                "totalCalories": {
                  "type": ["integer", "null"],
                  "description": "Total estimated calories"
                },
                "protein": {
                  "type": ["number", "null"],
                  "description": "Protein in grams"
                },
                "carbohydrates": {
                  "type": ["number", "null"],
                  "description": "Carbohydrates in grams"
                },
                "fat": {
                  "type": ["number", "null"],
                  "description": "Fat in grams"
                },
                "fiber": {
                  "type": ["number", "null"],
                  "description": "Fiber in grams"
                },
                "sugar": {
                  "type": ["number", "null"],
                  "description": "Sugar in grams"
                },
                "sodium": {
                  "type": ["number", "null"],
                  "description": "Sodium in milligrams"
                }
              },
              "required": ["totalCalories", "protein", "carbohydrates", "fat", "fiber", "sugar", "sodium"],
              "additionalProperties": false
            },
            "healthInsights": {
              "type": ["object", "null"],
              "description": "Overall health assessment",
              "properties": {
                "healthScore": {
                  "type": ["number", "null"],
                  "description": "Health score (0-10, where 10 is healthiest)"
                },
                "summary": {
                  "type": ["string", "null"],
                  "description": "Brief summary of health characteristics"
                },
                "positives": {
                  "type": "array",
                  "description": "Positive aspects of the meal",
                  "items": {
                    "type": "string"
                  }
                },
                "improvements": {
                  "type": "array",
                  "description": "Areas for improvement",
                  "items": {
                    "type": "string"
                  }
                },
                "recommendations": {
                  "type": "array",
                  "description": "Specific recommendations",
                  "items": {
                    "type": "string"
                  }
                }
              },
              "required": ["healthScore", "summary", "positives", "improvements", "recommendations"],
              "additionalProperties": false
            },
            "confidence": {
              "type": "number",
              "description": "Overall confidence in the analysis (0.0 to 1.0)"
            },
            "warnings": {
              "type": "array",
              "description": "Any warnings or errors",
              "items": {
                "type": "string"
              }
            }
          },
          "required": ["schemaVersion", "foodItems", "nutrition", "healthInsights", "confidence", "warnings"],
          "additionalProperties": false
        }
        """;
    }
}
