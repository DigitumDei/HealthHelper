using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Services.Llm;
using Microsoft.Extensions.Logging;

namespace HealthHelper.Services.Analysis;

public interface IAnalysisOrchestrator
{
    Task<AnalysisInvocationResult> ProcessEntryAsync(TrackedEntry entry, CancellationToken cancellationToken = default);
}

public class AnalysisOrchestrator : IAnalysisOrchestrator
{
    private const string DefaultOpenAiVisionModel = "gpt-4o-mini";

    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IEntryAnalysisRepository _entryAnalysisRepository;
    private readonly ILLmClient _llmClient;
    private readonly MealAnalysisValidator _validator;
    private readonly ILogger<AnalysisOrchestrator> _logger;

    public AnalysisOrchestrator(
        IAppSettingsRepository appSettingsRepository,
        IEntryAnalysisRepository entryAnalysisRepository,
        ILLmClient llmClient,
        MealAnalysisValidator validator,
        ILogger<AnalysisOrchestrator> logger)
    {
        _appSettingsRepository = appSettingsRepository;
        _entryAnalysisRepository = entryAnalysisRepository;
        _llmClient = llmClient;
        _validator = validator;
        _logger = logger;
    }

    public async Task<AnalysisInvocationResult> ProcessEntryAsync(TrackedEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        try
        {
            var settings = await _appSettingsRepository.GetAppSettingsAsync().ConfigureAwait(false);

            if (settings.SelectedProvider != LlmProvider.OpenAI)
            {
                _logger.LogInformation("Selected provider {Provider} is not yet supported; skipping analysis for entry {EntryId}.", settings.SelectedProvider, entry.EntryId);
                return AnalysisInvocationResult.NotSupported(settings.SelectedProvider);
            }

            var modelId = ResolveModelId(settings);
            if (string.IsNullOrWhiteSpace(modelId))
            {
                _logger.LogWarning("No model configured for provider {Provider}; skipping analysis for entry {EntryId}.", settings.SelectedProvider, entry.EntryId);
                return AnalysisInvocationResult.MissingModel(settings.SelectedProvider);
            }

            if (!settings.ApiKeys.TryGetValue(settings.SelectedProvider, out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("No API key configured for provider {Provider}; skipping analysis for entry {EntryId}.", settings.SelectedProvider, entry.EntryId);
                return AnalysisInvocationResult.MissingCredentials(settings.SelectedProvider);
            }

            var context = new LlmRequestContext
            {
                ModelId = modelId,
                Provider = settings.SelectedProvider,
                ApiKey = apiKey
            };

            var llmResult = await _llmClient.InvokeAnalysisAsync(entry, context).ConfigureAwait(false);
            if (llmResult.Analysis is null)
            {
                _logger.LogWarning("LLM returned no analysis for entry {EntryId}.", entry.EntryId);
                return AnalysisInvocationResult.NoAnalysis();
            }

            // Validate the structured response
            var validationResult = _validator.Validate(llmResult.Analysis.InsightsJson, llmResult.Analysis.SchemaVersion);
            if (!validationResult.IsValid)
            {
                _logger.LogError("Analysis validation failed for entry {EntryId}. Errors: {Errors}",
                    entry.EntryId,
                    string.Join("; ", validationResult.Errors));
            }
            else if (validationResult.Warnings.Any())
            {
                _logger.LogWarning("Analysis validation warnings for entry {EntryId}: {Warnings}",
                    entry.EntryId,
                    string.Join("; ", validationResult.Warnings));
            }

            llmResult.Analysis.EntryId = entry.EntryId;

            await _entryAnalysisRepository.AddAsync(llmResult.Analysis).ConfigureAwait(false);

            if (llmResult.Diagnostics is not null)
            {
                _logger.LogInformation(
                    "Stored analysis for entry {EntryId} using model {Model}. Tokens used: prompt={PromptTokens}, completion={CompletionTokens}, total={TotalTokens}.",
                    entry.EntryId,
                    llmResult.Analysis.Model,
                    llmResult.Diagnostics.PromptTokenCount,
                    llmResult.Diagnostics.CompletionTokenCount,
                    llmResult.Diagnostics.TotalTokenCount);
            }

            return AnalysisInvocationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process LLM analysis for entry {EntryId}.", entry.EntryId);
            return AnalysisInvocationResult.Error();
        }
    }

    private static string ResolveModelId(AppSettings settings)
    {
        var configuredModel = settings.GetModelPreference(settings.SelectedProvider);
        if (!string.IsNullOrWhiteSpace(configuredModel))
        {
            return configuredModel;
        }

        return settings.SelectedProvider switch
        {
            LlmProvider.OpenAI => DefaultOpenAiVisionModel,
            _ => string.Empty
        };
    }
}

public class AnalysisInvocationResult
{
    private AnalysisInvocationResult(bool isQueued, string? userMessage = null, bool requiresCredentials = false)
    {
        IsQueued = isQueued;
        UserMessage = userMessage;
        RequiresCredentials = requiresCredentials;
    }

    public bool IsQueued { get; }
    public string? UserMessage { get; }
    public bool RequiresCredentials { get; }

    public static AnalysisInvocationResult Success() => new(true);
    public static AnalysisInvocationResult MissingCredentials(LlmProvider provider) => new(false, $"Add an API key for {provider} to enable meal analysis.", true);
    public static AnalysisInvocationResult MissingModel(LlmProvider provider) => new(false, $"Select a model for {provider} before running meal analysis.");
    public static AnalysisInvocationResult NotSupported(LlmProvider provider) => new(false, $"{provider} is not supported yet. Switch providers in Settings to analyze meals.");
    public static AnalysisInvocationResult NoAnalysis() => new(false, "The analysis service did not return results. Try again later.");
    public static AnalysisInvocationResult Error() => new(false, "Meal analysis failed. You can retry from the settings page.");
}
