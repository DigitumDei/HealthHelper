using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Services.Llm;
using Microsoft.Extensions.Logging;

namespace HealthHelper.Services.Analysis;

public interface IAnalysisOrchestrator
{
    Task<AnalysisInvocationResult> ProcessEntryAsync(TrackedEntry entry, CancellationToken cancellationToken = default);
    Task<AnalysisInvocationResult> ProcessCorrectionAsync(TrackedEntry entry, EntryAnalysis existingAnalysis, string correction, CancellationToken cancellationToken = default);
}

public class AnalysisOrchestrator : IAnalysisOrchestrator
{
    private const string DefaultOpenAiVisionModel = "gpt-5-mini";

    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IEntryAnalysisRepository _entryAnalysisRepository;
    private readonly IDailySummaryService _dailySummaryService;
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly ILLmClient _llmClient;
    private readonly MealAnalysisValidator _validator;
    private readonly ILogger<AnalysisOrchestrator> _logger;

    public AnalysisOrchestrator(
        IAppSettingsRepository appSettingsRepository,
        IEntryAnalysisRepository entryAnalysisRepository,
        IDailySummaryService dailySummaryService,
        ITrackedEntryRepository trackedEntryRepository,
        ILLmClient llmClient,
        MealAnalysisValidator validator,
        ILogger<AnalysisOrchestrator> logger)
    {
        _appSettingsRepository = appSettingsRepository;
        _entryAnalysisRepository = entryAnalysisRepository;
        _dailySummaryService = dailySummaryService;
        _trackedEntryRepository = trackedEntryRepository;
        _llmClient = llmClient;
        _validator = validator;
        _logger = logger;
    }

    public Task<AnalysisInvocationResult> ProcessEntryAsync(TrackedEntry entry, CancellationToken cancellationToken = default)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        if (string.Equals(entry.EntryType, "DailySummary", StringComparison.OrdinalIgnoreCase))
        {
            return _dailySummaryService.GenerateAsync(entry, cancellationToken);
        }

        return ProcessUnifiedEntryAsync(entry, existingAnalysis: null, correction: null, cancellationToken);
    }

    public Task<AnalysisInvocationResult> ProcessCorrectionAsync(TrackedEntry entry, EntryAnalysis existingAnalysis, string correction, CancellationToken cancellationToken = default)
    {
        if (existingAnalysis is null)
        {
            throw new ArgumentNullException(nameof(existingAnalysis));
        }

        if (string.IsNullOrWhiteSpace(correction))
        {
            _logger.LogWarning("Correction text was empty for entry {EntryId}.", entry.EntryId);
            return Task.FromResult(AnalysisInvocationResult.Error());
        }

        if (string.Equals(entry.EntryType, "DailySummary", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Corrections are not supported for daily summary entries ({EntryId}).", entry.EntryId);
            return Task.FromResult(AnalysisInvocationResult.Error());
        }

        return ProcessUnifiedEntryAsync(entry, existingAnalysis, correction, cancellationToken);
    }

    private async Task<AnalysisInvocationResult> ProcessUnifiedEntryAsync(
        TrackedEntry entry,
        EntryAnalysis? existingAnalysis,
        string? correction,
        CancellationToken cancellationToken)
    {
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

            var llmResult = await _llmClient.InvokeAnalysisAsync(entry, context, existingAnalysis?.InsightsJson, correction).ConfigureAwait(false);
            if (llmResult.Analysis is null)
            {
                _logger.LogWarning("LLM returned no analysis for entry {EntryId}.", entry.EntryId);
                return AnalysisInvocationResult.NoAnalysis();
            }

            llmResult.Analysis.EntryId = entry.EntryId;
            llmResult.Analysis.CapturedAt = DateTime.UtcNow;

            UnifiedAnalysisResult? unifiedResult = null;
            try
            {
                unifiedResult = JsonSerializer.Deserialize<UnifiedAnalysisResult>(llmResult.Analysis.InsightsJson);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogWarning(jsonEx, "Failed to deserialize unified analysis for entry {EntryId}.", entry.EntryId);
            }

            if (unifiedResult is not null)
            {
                await UpdateEntryTypeIfNeededAsync(entry, unifiedResult).ConfigureAwait(false);
                ValidateMealAnalysis(entry.EntryId, unifiedResult);
            }

            if (existingAnalysis is null)
            {
                await _entryAnalysisRepository.AddAsync(llmResult.Analysis).ConfigureAwait(false);
            }
            else
            {
                llmResult.Analysis.AnalysisId = existingAnalysis.AnalysisId;
                llmResult.Analysis.ExternalId = existingAnalysis.ExternalId;
                await _entryAnalysisRepository.UpdateAsync(llmResult.Analysis).ConfigureAwait(false);
            }

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
            _logger.LogError(ex, "Failed to process analysis for entry {EntryId}.", entry.EntryId);
            return AnalysisInvocationResult.Error();
        }
    }

    private async Task UpdateEntryTypeIfNeededAsync(TrackedEntry entry, UnifiedAnalysisResult unified)
    {
        var detectedType = NormalizeEntryType(unified.EntryType);
        if (string.IsNullOrWhiteSpace(detectedType))
        {
            return;
        }

        if (string.Equals(entry.EntryType, detectedType, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _logger.LogInformation("Detected entry type {DetectedType} for entry {EntryId} (was {ExistingType}).", detectedType, entry.EntryId, entry.EntryType);
        entry.EntryType = detectedType;
        await _trackedEntryRepository.UpdateEntryTypeAsync(entry.EntryId, detectedType).ConfigureAwait(false);
    }

    private void ValidateMealAnalysis(int entryId, UnifiedAnalysisResult unified)
    {
        if (!string.Equals(unified.EntryType, "Meal", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (unified.MealAnalysis is null)
        {
            _logger.LogWarning("Meal entry {EntryId} did not include mealAnalysis payload.", entryId);
            return;
        }

        try
        {
            var mealJson = JsonSerializer.Serialize(unified.MealAnalysis);
            var validation = _validator.Validate(mealJson, unified.MealAnalysis.SchemaVersion);
            if (!validation.IsValid)
            {
                _logger.LogWarning("Meal analysis validation failed for entry {EntryId}: {Errors}", entryId, string.Join("; ", validation.Errors));
            }
            else if (validation.Warnings.Count > 0)
            {
                _logger.LogInformation("Meal analysis warnings for entry {EntryId}: {Warnings}", entryId, string.Join("; ", validation.Warnings));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to validate meal analysis for entry {EntryId}.", entryId);
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

    private static string? NormalizeEntryType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (string.Equals(value, "Meal", StringComparison.OrdinalIgnoreCase))
        {
            return "Meal";
        }

        if (string.Equals(value, "Exercise", StringComparison.OrdinalIgnoreCase))
        {
            return "Exercise";
        }

        if (string.Equals(value, "Sleep", StringComparison.OrdinalIgnoreCase))
        {
            return "Sleep";
        }

        if (string.Equals(value, "Other", StringComparison.OrdinalIgnoreCase))
        {
            return "Other";
        }

        return null;
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
    public static AnalysisInvocationResult MissingCredentials(LlmProvider provider) => new(false, $"Add an API key for {provider} to enable analysis.", true);
    public static AnalysisInvocationResult MissingModel(LlmProvider provider) => new(false, $"Select a model for {provider} before running analysis.");
    public static AnalysisInvocationResult NotSupported(LlmProvider provider) => new(false, $"{provider} is not supported yet. Switch providers in Settings to analyze entries.");
    public static AnalysisInvocationResult NoAnalysis() => new(false, "The analysis service did not return results. Try again later.");
    public static AnalysisInvocationResult Error() => new(false, "Analysis failed. You can retry from the settings page.");
}
