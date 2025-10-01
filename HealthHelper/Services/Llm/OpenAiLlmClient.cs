using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HealthHelper.Models;
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

    public async Task<LlmAnalysisResult> InvokeAnalysisAsync(TrackedEntry entry, LlmRequestContext context)
    {
        if (string.IsNullOrWhiteSpace(context.ApiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        var client = new OpenAIClient(context.ApiKey);
        var chatClient = client.GetChatClient(context.ModelId);

        var messages = await CreateChatRequest(entry);

        try
        {
            var options = new ChatCompletionOptions
            {
                ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                    jsonSchemaFormatName: "meal_analysis",
                    jsonSchema: BinaryData.FromString(GetMealAnalysisSchema()),
                    jsonSchemaIsStrict: true
                )
            };

            var response = await chatClient.CompleteChatAsync(messages, options);

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

    private static async Task<List<ChatMessage>> CreateChatRequest(TrackedEntry entry)
    {
        var systemPrompt = @"You are a helpful assistant that analyzes meal photos.
Identify all food items, estimate portion sizes, and provide nutritional information.
Give an overall health assessment with specific recommendations.
Be accurate but acknowledge uncertainty when applicable.
Return your analysis in the structured JSON format specified.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
        };

        var userMessageContent = new List<ChatMessageContentPart>
        {
            ChatMessageContentPart.CreateTextPart("Analyze this meal.")
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
