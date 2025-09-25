using HealthHelper.Data;
using HealthHelper.Models;
using Microsoft.Extensions.Logging;
using OpenAI;
using OpenAI.Chat;
namespace HealthHelper.Services.Llm;

public class OpenAiLlmClient : ILLmClient
{
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly ILogger<OpenAiLlmClient> _logger;

    public OpenAiLlmClient(IAppSettingsRepository appSettingsRepository, ILogger<OpenAiLlmClient> logger)
    {
        _appSettingsRepository = appSettingsRepository;
        _logger = logger;
    }

    public async Task<LlmAnalysisResult> InvokeAnalysisAsync(TrackedEntry entry, LlmRequestContext context)
    {
        var settings = await _appSettingsRepository.GetAppSettingsAsync();
        if (!settings.ApiKeys.TryGetValue(LlmProvider.OpenAI, out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        var client = new OpenAIClient(apiKey);
        var chatClient = client.GetChatClient(context.ModelId);

        var messages = await CreateChatRequest(entry);

        try
        {
            var response = await chatClient.CompleteChatAsync(messages);

            var analysis = new EntryAnalysis
            {
                EntryId = entry.EntryId,
                ProviderId = LlmProvider.OpenAI.ToString(),
                Model = context.ModelId,
                CapturedAt = DateTime.UtcNow,
                InsightsJson = response.Value.Content.ToString() ?? string.Empty
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
        if (!string.IsNullOrEmpty(entry.BlobPath) && File.Exists(absoluteBlobPath))
        {
            var imageBytes = await File.ReadAllBytesAsync(absoluteBlobPath);
            userMessageContent.Add(ChatMessageContentPart.CreateImagePart(new BinaryData(imageBytes), "image/png"));
        }

        messages.Add(new UserChatMessage(userMessageContent));

        return messages;
    }
}