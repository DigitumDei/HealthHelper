using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Services.Analysis;
using HealthHelper.Services.Platform;
using HealthHelper.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace HealthHelper.PageModels;

[QueryProperty(nameof(SummaryEntryId), "SummaryEntryId")]
public partial class DailySummaryViewModel : ObservableObject
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly IEntryAnalysisRepository _entryAnalysisRepository;
    private readonly IBackgroundAnalysisService _backgroundAnalysisService;
    private readonly INotificationPermissionService _notificationPermissionService;
    private readonly ILogger<DailySummaryViewModel> _logger;

    [ObservableProperty]
    private int summaryEntryId;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    [ObservableProperty]
    private ProcessingStatus processingStatus;

    [ObservableProperty]
    private string totalsSummary = string.Empty;

    [ObservableProperty]
    private string balanceOverall = string.Empty;

    [ObservableProperty]
    private string balanceMacro = string.Empty;

    [ObservableProperty]
    private string balanceTiming = string.Empty;

    [ObservableProperty]
    private string balanceVariety = string.Empty;

    [ObservableProperty]
    private string generatedAtText = string.Empty;

    public ObservableCollection<string> Insights { get; } = new();
    public ObservableCollection<string> Recommendations { get; } = new();
    public ObservableCollection<DailySummaryEntryReference> EntriesIncluded { get; } = new();

    public DailySummaryViewModel(
        ITrackedEntryRepository trackedEntryRepository,
        IEntryAnalysisRepository entryAnalysisRepository,
        IBackgroundAnalysisService backgroundAnalysisService,
        INotificationPermissionService notificationPermissionService,
        ILogger<DailySummaryViewModel> logger)
    {
        _trackedEntryRepository = trackedEntryRepository;
        _entryAnalysisRepository = entryAnalysisRepository;
        _backgroundAnalysisService = backgroundAnalysisService;
        _notificationPermissionService = notificationPermissionService;
        _logger = logger;
    }

    partial void OnSummaryEntryIdChanged(int value)
    {
        if (value > 0)
        {
            _ = LoadSummaryAsync();
        }
    }

    private async Task LoadSummaryAsync()
    {
        if (SummaryEntryId <= 0 || IsBusy)
        {
            return;
        }

        try
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsBusy = true;
                StatusMessage = string.Empty;
                Insights.Clear();
                Recommendations.Clear();
                EntriesIncluded.Clear();
            });

            var entry = await _trackedEntryRepository.GetByIdAsync(SummaryEntryId).ConfigureAwait(false);
            if (entry is null)
            {
                StatusMessage = "We couldn't find today's summary entry.";
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ProcessingStatus = entry.ProcessingStatus;

                var payload = entry.Payload as DailySummaryPayload ?? new DailySummaryPayload();
                var hasExplicitGeneratedAt = payload.GeneratedAt != default;
                var generatedAtUtc = hasExplicitGeneratedAt ? payload.GeneratedAt : entry.CapturedAt;
                var generatedAtTimeZoneId = hasExplicitGeneratedAt
                    ? payload.GeneratedAtTimeZoneId ?? entry.CapturedAtTimeZoneId
                    : entry.CapturedAtTimeZoneId;
                var generatedAtOffsetMinutes = hasExplicitGeneratedAt
                    ? payload.GeneratedAtOffsetMinutes ?? entry.CapturedAtOffsetMinutes
                    : entry.CapturedAtOffsetMinutes;

                var generatedAtLocal = DateTimeConverter.ToOriginalLocal(
                    generatedAtUtc,
                    generatedAtTimeZoneId,
                    generatedAtOffsetMinutes);

                GeneratedAtText = string.Format(
                    CultureInfo.CurrentCulture,
                    "Generated {0}",
                    generatedAtLocal.ToString("f", CultureInfo.CurrentCulture));
            });

            var analysis = await _entryAnalysisRepository.GetByTrackedEntryIdAsync(SummaryEntryId).ConfigureAwait(false);
            if (analysis is null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    StatusMessage = ProcessingStatus is ProcessingStatus.Pending or ProcessingStatus.Processing
                        ? "Your summary is still processing. Please check back soon."
                        : "No analysis data is available for this summary yet.";
                });
                return;
            }

            DailySummaryResult? summaryResult = null;
            try
            {
                summaryResult = JsonSerializer.Deserialize<DailySummaryResult>(analysis.InsightsJson);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialize daily summary JSON for entry {EntryId}.", SummaryEntryId);
            }

            if (summaryResult is null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    StatusMessage = "We couldn't parse the summary details.";
                });
                return;
            }

            await MainThread.InvokeOnMainThreadAsync(() => PopulateSummary(summaryResult));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load daily summary {EntryId}.", SummaryEntryId);
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                StatusMessage = "We couldn't load the summary right now. Try again later.";
            });
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() => IsBusy = false);
        }
    }

    private void PopulateSummary(DailySummaryResult summaryResult)
    {
        var totals = summaryResult.Totals ?? new NutritionTotals();
        var balance = summaryResult.Balance ?? new NutritionalBalance();

        TotalsSummary = BuildTotalsSummary(totals);
        BalanceOverall = balance.Overall ?? string.Empty;
        BalanceMacro = balance.MacroBalance ?? string.Empty;
        BalanceTiming = balance.Timing ?? string.Empty;
        BalanceVariety = balance.Variety ?? string.Empty;

        foreach (var insight in (summaryResult.Insights ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            Insights.Add(insight);
        }

        foreach (var recommendation in (summaryResult.Recommendations ?? new List<string>()).Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            Recommendations.Add(recommendation);
        }

        foreach (var entry in summaryResult.EntriesIncluded ?? new List<DailySummaryEntryReference>())
        {
            EntriesIncluded.Add(entry);
        }
    }

    private static string BuildTotalsSummary(NutritionTotals totals)
    {
        totals ??= new NutritionTotals();

        static string FormatValue(double? value, string unit)
        {
            return value.HasValue ? $"{value.Value:0.#}{unit}" : "—";
        }

        return $"Calories: {FormatValue(totals.Calories, " kcal")}\n" +
               $"Protein: {FormatValue(totals.Protein, " g")}\n" +
               $"Carbs: {FormatValue(totals.Carbohydrates, " g")}\n" +
               $"Fat: {FormatValue(totals.Fat, " g")}\n" +
               $"Fiber: {FormatValue(totals.Fiber, " g")}\n" +
               $"Sugar: {FormatValue(totals.Sugar, " g")}\n" +
               $"Sodium: {FormatValue(totals.Sodium, " mg")}";
    }

    [RelayCommand]
    private async Task RegenerateAsync()
    {
        if (SummaryEntryId <= 0)
        {
            return;
        }

        try
        {
            var entry = await _trackedEntryRepository.GetByIdAsync(SummaryEntryId).ConfigureAwait(false);
            if (entry is null)
            {
                await ShowAlertAsync("Summary missing", "We couldn't find today's summary entry.");
                return;
            }

            var entryTimeZone = DateTimeConverter.ResolveTimeZone(entry.CapturedAtTimeZoneId, entry.CapturedAtOffsetMinutes);
            var entryLocalDate = DateTimeConverter.ToOriginalLocal(entry.CapturedAt, entry.CapturedAtTimeZoneId, entry.CapturedAtOffsetMinutes, entryTimeZone);

            var dayEntries = await _trackedEntryRepository
                .GetByDayAsync(entryLocalDate, entryTimeZone)
                .ConfigureAwait(false);
            var entryCount = dayEntries
                .Where(e => e.EntryType != EntryType.DailySummary)
                .Count(e => e.ProcessingStatus == ProcessingStatus.Completed);

            var regenerateCapturedAtUtc = DateTime.UtcNow;
            var (timeZoneId, offsetMinutes) = DateTimeConverter.CaptureTimeZoneMetadata(regenerateCapturedAtUtc);

            var payload = new DailySummaryPayload
            {
                SchemaVersion = 1,
                EntryCount = entryCount,
                GeneratedAt = regenerateCapturedAtUtc,
                GeneratedAtTimeZoneId = timeZoneId,
                GeneratedAtOffsetMinutes = offsetMinutes
            };
            entry.Payload = payload;
            entry.CapturedAt = regenerateCapturedAtUtc;
            entry.CapturedAtTimeZoneId = timeZoneId;
            entry.CapturedAtOffsetMinutes = offsetMinutes;
            entry.ProcessingStatus = ProcessingStatus.Pending;
            entry.DataSchemaVersion = payload.SchemaVersion;

            await _trackedEntryRepository.UpdateAsync(entry).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                ProcessingStatus = ProcessingStatus.Pending;
                StatusMessage = "Regenerating summary...";
            });

            // Request notification permission to ensure background execution works
            await _notificationPermissionService.EnsurePermissionAsync();

            await _backgroundAnalysisService.QueueEntryAsync(entry.EntryId).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to regenerate daily summary {EntryId}.", SummaryEntryId);
            await ShowAlertAsync("Regeneration failed", "We couldn't regenerate the summary. Try again later.");
        }
    }

    private async Task ShowAlertAsync(string title, string message)
    {
        await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync(title, message, "OK"));
    }
}
