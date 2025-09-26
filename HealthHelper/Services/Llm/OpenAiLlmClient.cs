using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
            var response = await chatClient.CompleteChatAsync(messages);

            var insights = ExtractTextContent(response.Value.Content);

            var analysis = new EntryAnalysis
            {
                EntryId = entry.EntryId,
                ProviderId = LlmProvider.OpenAI.ToString(),
                Model = context.ModelId,
                CapturedAt = DateTime.UtcNow,
                InsightsJson = insights
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
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("You are a helpful assistant that analyzes meal photos. Provide a nutritional estimate and identify the food items."),
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
}
