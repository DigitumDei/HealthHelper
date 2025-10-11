using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Services.Llm;
using HealthHelper.Utilities;
using Microsoft.Extensions.Logging;

namespace HealthHelper.Services.Analysis;

public interface IDailySummaryService
{
    Task<AnalysisInvocationResult> GenerateAsync(TrackedEntry summaryEntry, CancellationToken cancellationToken = default);
}

public class DailySummaryService : IDailySummaryService
{
    private const string DefaultOpenAiModel = "gpt-5-mini";

    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly IEntryAnalysisRepository _entryAnalysisRepository;
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly ILLmClient _llmClient;
    private readonly ILogger<DailySummaryService> _logger;

    public DailySummaryService(
        IAppSettingsRepository appSettingsRepository,
        IEntryAnalysisRepository entryAnalysisRepository,
        ITrackedEntryRepository trackedEntryRepository,
        ILLmClient llmClient,
        ILogger<DailySummaryService> logger)
    {
        _appSettingsRepository = appSettingsRepository;
        _entryAnalysisRepository = entryAnalysisRepository;
        _trackedEntryRepository = trackedEntryRepository;
        _llmClient = llmClient;
        _logger = logger;
    }

    public async Task<AnalysisInvocationResult> GenerateAsync(TrackedEntry summaryEntry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(summaryEntry);

        try
        {
            var settings = await _appSettingsRepository.GetAppSettingsAsync().ConfigureAwait(false);

            if (settings.SelectedProvider != LlmProvider.OpenAI)
            {
                _logger.LogInformation("Provider {Provider} is not supported for daily summaries.", settings.SelectedProvider);
                return AnalysisInvocationResult.NotSupported(settings.SelectedProvider);
            }

            var modelId = ResolveModelId(settings);
            if (string.IsNullOrWhiteSpace(modelId))
            {
                _logger.LogWarning("Daily summary skipped because no model is configured for provider {Provider}.", settings.SelectedProvider);
                return AnalysisInvocationResult.MissingModel(settings.SelectedProvider);
            }

            if (!settings.ApiKeys.TryGetValue(settings.SelectedProvider, out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("Daily summary skipped because API key for provider {Provider} is missing.", settings.SelectedProvider);
                return AnalysisInvocationResult.MissingCredentials(settings.SelectedProvider);
            }

            var summaryTimeZone = DateTimeConverter.ResolveTimeZone(summaryEntry.CapturedAtTimeZoneId, summaryEntry.CapturedAtOffsetMinutes);
            var summaryLocalDate = DateTimeConverter.ToOriginalLocal(summaryEntry.CapturedAt, summaryEntry.CapturedAtTimeZoneId, summaryEntry.CapturedAtOffsetMinutes, summaryTimeZone);
            var summaryOffsetMinutes = summaryEntry.CapturedAtOffsetMinutes
                ?? (summaryTimeZone is not null ? DateTimeConverter.GetUtcOffsetMinutes(summaryTimeZone, summaryEntry.CapturedAt) : (int?)null);

            var mealsForDay = await _trackedEntryRepository
                .GetByEntryTypeAndDayAsync("Meal", summaryLocalDate, summaryTimeZone)
                .ConfigureAwait(false);

            var completedMealEntries = mealsForDay
                .Where(entry => entry.ProcessingStatus == ProcessingStatus.Completed)
                .OrderBy(entry => entry.CapturedAt)
                .ToList();

            var analyses = await _entryAnalysisRepository
                .ListByDayAsync(summaryLocalDate, summaryTimeZone)
                .ConfigureAwait(false);

            var analysesByEntry = analyses
                .Where(a => a.EntryId != summaryEntry.EntryId)
                .GroupBy(a => a.EntryId)
                .ToDictionary(group => group.Key, group => group.OrderByDescending(a => a.CapturedAt).First());

            var summaryRequest = new DailySummaryRequest
            {
                SummaryEntryId = summaryEntry.EntryId,
                SummaryDate = summaryLocalDate.Date,
                SummaryTimeZoneId = summaryEntry.CapturedAtTimeZoneId,
                SummaryUtcOffsetMinutes = summaryOffsetMinutes
            };

            foreach (var mealEntry in completedMealEntries)
            {
                analysesByEntry.TryGetValue(mealEntry.EntryId, out var analysisEntry);

                MealAnalysisResult? structuredAnalysis = null;
                if (analysisEntry is not null)
                {
                    try
                    {
                        structuredAnalysis = JsonSerializer.Deserialize<MealAnalysisResult>(analysisEntry.InsightsJson);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize meal analysis for entry {EntryId} when generating daily summary.", mealEntry.EntryId);
                    }
                }

                var description = (mealEntry.Payload as MealPayload)?.Description;

                var mealTimeZone = DateTimeConverter.ResolveTimeZone(mealEntry.CapturedAtTimeZoneId, mealEntry.CapturedAtOffsetMinutes);
                var mealCapturedAtLocal = DateTimeConverter.ToOriginalLocal(mealEntry.CapturedAt, mealEntry.CapturedAtTimeZoneId, mealEntry.CapturedAtOffsetMinutes, mealTimeZone);
                var mealOffsetMinutes = mealEntry.CapturedAtOffsetMinutes
                    ?? (mealTimeZone is not null ? DateTimeConverter.GetUtcOffsetMinutes(mealTimeZone, mealEntry.CapturedAt) : (int?)null);

                summaryRequest.Meals.Add(new DailySummaryMealContext
                {
                    EntryId = mealEntry.EntryId,
                    CapturedAt = mealEntry.CapturedAt,
                    CapturedAtLocal = mealCapturedAtLocal,
                    TimeZoneId = mealEntry.CapturedAtTimeZoneId,
                    UtcOffsetMinutes = mealOffsetMinutes,
                    Description = description,
                    Analysis = structuredAnalysis
                });
            }

            var context = new LlmRequestContext
            {
                ModelId = modelId,
                Provider = settings.SelectedProvider,
                ApiKey = apiKey
            };

            var existingSummary = await _entryAnalysisRepository
                .GetByTrackedEntryIdAsync(summaryEntry.EntryId)
                .ConfigureAwait(false);

            var existingSummaryJson = existingSummary?.InsightsJson;

            var llmResult = await _llmClient
                .InvokeDailySummaryAsync(summaryRequest, context, existingSummaryJson)
                .ConfigureAwait(false);

            if (llmResult.Analysis is null)
            {
                _logger.LogWarning("LLM returned no daily summary analysis for entry {EntryId}.", summaryEntry.EntryId);
                return AnalysisInvocationResult.NoAnalysis();
            }

            if (existingSummary is null)
            {
                await _entryAnalysisRepository.AddAsync(llmResult.Analysis).ConfigureAwait(false);
            }
            else
            {
                llmResult.Analysis.AnalysisId = existingSummary.AnalysisId;
                llmResult.Analysis.ExternalId = existingSummary.ExternalId;
                await _entryAnalysisRepository.UpdateAsync(llmResult.Analysis).ConfigureAwait(false);
            }

            if (llmResult.Diagnostics is not null)
            {
                _logger.LogInformation(
                    "Stored daily summary for {SummaryDate}. Tokens used: prompt={PromptTokens}, completion={CompletionTokens}, total={TotalTokens}.",
                    summaryEntry.CapturedAt.ToString("yyyy-MM-dd"),
                    llmResult.Diagnostics.PromptTokenCount,
                    llmResult.Diagnostics.CompletionTokenCount,
                    llmResult.Diagnostics.TotalTokenCount);
            }

            return AnalysisInvocationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate daily summary for entry {EntryId}.", summaryEntry.EntryId);
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
            LlmProvider.OpenAI => DefaultOpenAiModel,
            _ => string.Empty
        };
    }
}
