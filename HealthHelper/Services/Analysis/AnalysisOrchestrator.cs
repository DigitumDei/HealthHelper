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

        if (entry.EntryType == EntryType.DailySummary)
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

        if (entry.EntryType == EntryType.DailySummary)
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
                var detectedType = NormalizeEntryType(unifiedResult.EntryType);
                if (detectedType is null)
                {
                    _logger.LogWarning("Unified analysis returned an unknown entry type for entry {EntryId}.", entry.EntryId);
                }
                else
                {
                    await UnifiedAnalysisApplier.ApplyAsync(entry, detectedType.Value, _trackedEntryRepository, _logger).ConfigureAwait(false);
                    ValidateUnifiedAnalysis(entry.EntryId, detectedType.Value, unifiedResult);
                }
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

    private void ValidateUnifiedAnalysis(int entryId, EntryType detectedType, UnifiedAnalysisResult unified)
    {
        switch (detectedType)
        {
            case EntryType.Meal:
                ValidateMealAnalysis(entryId, unified);
                break;
            case EntryType.Exercise:
                ValidateExerciseAnalysis(entryId, unified);
                break;
            case EntryType.Sleep:
                ValidateSleepAnalysis(entryId, unified);
                break;
            case EntryType.Other:
                ValidateOtherAnalysis(entryId, unified);
                break;
            default:
                _logger.LogWarning("Validation skipped for entry {EntryId}; unrecognized entry type {EntryType}.", entryId, unified.EntryType);
                break;
        }
    }

    private void ValidateMealAnalysis(int entryId, UnifiedAnalysisResult unified)
    {
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

    private void ValidateExerciseAnalysis(int entryId, UnifiedAnalysisResult unified)
    {
        if (unified.ExerciseAnalysis is null)
        {
            _logger.LogWarning("Exercise entry {EntryId} did not include exerciseAnalysis payload.", entryId);
            return;
        }

        if (string.IsNullOrWhiteSpace(unified.ExerciseAnalysis.SchemaVersion))
        {
            _logger.LogWarning("Exercise analysis for entry {EntryId} omitted schemaVersion.", entryId);
        }
    }

    private void ValidateSleepAnalysis(int entryId, UnifiedAnalysisResult unified)
    {
        if (unified.SleepAnalysis is null)
        {
            _logger.LogWarning("Sleep entry {EntryId} did not include sleepAnalysis payload.", entryId);
        }
    }

    private void ValidateOtherAnalysis(int entryId, UnifiedAnalysisResult unified)
    {
        if (unified.OtherAnalysis is null)
        {
            _logger.LogWarning("Other entry {EntryId} did not include otherAnalysis payload.", entryId);
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

    private static EntryType? NormalizeEntryType(string? value)
    {
        if (!EntryTypeHelper.TryParse(value, out var entryType))
        {
            return null;
        }

        return entryType;
    }
}
