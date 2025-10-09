using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Pages;
using HealthHelper.Services.Analysis;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace HealthHelper.PageModels;

public partial class MealLogViewModel : ObservableObject
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly IBackgroundAnalysisService _backgroundAnalysisService;
    private readonly ILogger<MealLogViewModel> _logger;
    public ObservableCollection<MealPhoto> Meals { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateDailySummaryCommand))]
    private DailySummaryCard? summaryCard;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateDailySummaryCommand))]
    private bool isGeneratingSummary;

    public bool ShowGenerateSummaryButton => SummaryCard is null;
    public bool ShowSummaryCard => SummaryCard is not null;

    public MealLogViewModel(ITrackedEntryRepository trackedEntryRepository, IBackgroundAnalysisService backgroundAnalysisService, ILogger<MealLogViewModel> logger)
    {
        _trackedEntryRepository = trackedEntryRepository;
        _backgroundAnalysisService = backgroundAnalysisService;
        _logger = logger;
    }

    partial void OnSummaryCardChanged(DailySummaryCard? value)
    {
        OnPropertyChanged(nameof(ShowGenerateSummaryButton));
        OnPropertyChanged(nameof(ShowSummaryCard));
    }

    [RelayCommand]
    private async Task GoToMealDetail(MealPhoto meal)
    {
        if (meal is null)
        {
            _logger.LogWarning("Attempted to navigate to meal details with a null meal reference.");
            return;
        }

        if (!meal.IsClickable)
        {
            _logger.LogInformation("Entry {EntryId} is not yet ready for viewing.", meal.EntryId);
            await Shell.Current.DisplayAlertAsync(
                "Still Processing",
                "This meal is still being analyzed. Please wait a moment.",
                "OK");
            return;
        }

        try
        {
            _logger.LogInformation("Navigating to meal detail for entry {EntryId}.", meal.EntryId);
            await Shell.Current.GoToAsync(nameof(MealDetailPage),
                new Dictionary<string, object>
                {
                    { "Meal", meal }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to meal detail for entry {EntryId}.", meal.EntryId);
            await Shell.Current.DisplayAlertAsync("Navigation error", "Unable to open meal details right now.", "OK");
        }
    }

    [RelayCommand(CanExecute = nameof(CanGenerateSummary))]
    private async Task GenerateDailySummaryAsync()
    {
        if (IsGeneratingSummary)
        {
            return;
        }

        try
        {
            IsGeneratingSummary = true;
            var mealCountSnapshot = Meals.Count;
            var summaryPayload = new DailySummaryPayload
            {
                MealCount = mealCountSnapshot,
                GeneratedAt = DateTime.UtcNow
            };

            var existingSummaryEntries = await _trackedEntryRepository
                .GetByEntryTypeAndDayAsync("DailySummary", DateTime.UtcNow)
                .ConfigureAwait(false);

            var existingSummary = existingSummaryEntries
                .OrderByDescending(entry => entry.CapturedAt)
                .FirstOrDefault();

            TrackedEntry summaryEntry;

            if (existingSummary is null)
            {
                summaryEntry = new TrackedEntry
                {
                    EntryType = "DailySummary",
                    CapturedAt = DateTime.UtcNow,
                    BlobPath = null,
                    Payload = summaryPayload,
                    DataSchemaVersion = 1,
                    ProcessingStatus = ProcessingStatus.Pending
                };

                await _trackedEntryRepository.AddAsync(summaryEntry).ConfigureAwait(false);
            }
            else
            {
                existingSummary.Payload = summaryPayload;
                existingSummary.CapturedAt = DateTime.UtcNow;
                existingSummary.ProcessingStatus = ProcessingStatus.Pending;
                existingSummary.DataSchemaVersion = existingSummary.DataSchemaVersion == 0 ? 1 : existingSummary.DataSchemaVersion;

                await _trackedEntryRepository.UpdateAsync(existingSummary).ConfigureAwait(false);
                summaryEntry = existingSummary;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                SummaryCard ??= new DailySummaryCard(summaryEntry.EntryId, summaryPayload.MealCount, summaryPayload.GeneratedAt, summaryEntry.ProcessingStatus);
                SummaryCard.RefreshMetadata(summaryPayload.MealCount, summaryPayload.GeneratedAt);
                SummaryCard.ProcessingStatus = ProcessingStatus.Pending;
                SummaryCard.IsOutdated = false;
                GenerateDailySummaryCommand.NotifyCanExecuteChanged();
            });

            await _backgroundAnalysisService.QueueEntryAsync(summaryEntry.EntryId).ConfigureAwait(false);
            _logger.LogInformation("Queued daily summary generation for entry {EntryId}.", summaryEntry.EntryId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate daily summary.");
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync(
                "Summary failed",
                "We couldn't start the daily summary. Try again later.",
                "OK"));
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                IsGeneratingSummary = false;
                GenerateDailySummaryCommand.NotifyCanExecuteChanged();
            });
        }
    }

    private bool CanGenerateSummary()
    {
        if (IsGeneratingSummary)
        {
            return false;
        }

        if (SummaryCard is null)
        {
            return true;
        }

        return SummaryCard.ProcessingStatus is not ProcessingStatus.Pending and not ProcessingStatus.Processing;
    }

    [RelayCommand]
    private async Task ViewDailySummaryAsync()
    {
        if (SummaryCard is null)
        {
            return;
        }

        if (!SummaryCard.IsClickable)
        {
            await Shell.Current.DisplayAlertAsync(
                "Summary Pending",
                "Your daily summary is still processing. Please try again shortly.",
                "OK");
            return;
        }

        await Shell.Current.GoToAsync(nameof(DailySummaryPage), new Dictionary<string, object>
        {
            { "SummaryEntryId", SummaryCard.EntryId }
        });
    }

    public async Task LoadEntriesAsync()
    {
        try
        {
            _logger.LogDebug("Loading meal entries for {Date}.", DateTime.UtcNow.Date);
            var entries = await _trackedEntryRepository.GetByDayAsync(DateTime.UtcNow).ConfigureAwait(false);

            var summaryEntries = await _trackedEntryRepository
                .GetByEntryTypeAndDayAsync("DailySummary", DateTime.UtcNow)
                .ConfigureAwait(false);

            var summaryEntry = summaryEntries
                .OrderByDescending(entry => entry.CapturedAt)
                .FirstOrDefault();

            DailySummaryCard? summaryCard = null;
            if (summaryEntry is not null)
            {
                var payload = summaryEntry.Payload as DailySummaryPayload ?? new DailySummaryPayload();
                summaryCard = new DailySummaryCard(summaryEntry.EntryId, payload.MealCount, payload.GeneratedAt, summaryEntry.ProcessingStatus);
            }

            var mealEntries = entries
                .Where(entry => string.Equals(entry.EntryType, "Meal", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var mealPhotos = mealEntries
                .Where(entry => entry.BlobPath is not null && entry.Payload is MealPayload)
                .OrderByDescending(entry => entry.CapturedAt)
                .Select(entry =>
                {
                    var mealPayload = (MealPayload)entry.Payload!;
                    var originalRelativePath = entry.BlobPath;
                    var displayPathRelative = mealPayload.PreviewBlobPath ?? originalRelativePath;

                    if (string.IsNullOrWhiteSpace(originalRelativePath) || string.IsNullOrWhiteSpace(displayPathRelative))
                    {
                        _logger.LogWarning("Skipping entry {EntryId} because file paths are missing.", entry.EntryId);
                        return null;
                    }

                    var displayFullPath = Path.Combine(FileSystem.AppDataDirectory, displayPathRelative);
                    var originalFullPath = Path.Combine(FileSystem.AppDataDirectory, originalRelativePath);
                    return new MealPhoto(entry.EntryId, displayFullPath, originalFullPath, mealPayload.Description ?? string.Empty, entry.CapturedAt, entry.ProcessingStatus);
                })
                .OfType<MealPhoto>()
                .ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Meals.Clear();
                foreach (var mealPhoto in mealPhotos)
                {
                    Meals.Add(mealPhoto);
                }

                if (summaryCard is not null)
                {
                    var isNewCard = SummaryCard is null || SummaryCard.EntryId != summaryCard.EntryId;
                    if (isNewCard)
                    {
                        SummaryCard = summaryCard;
                    }
                    else if (SummaryCard is not null)
                    {
                        SummaryCard.RefreshMetadata(summaryCard.MealCount, summaryCard.GeneratedAt);
                        SummaryCard.ProcessingStatus = summaryCard.ProcessingStatus;
                    }
                }
                else
                {
                    SummaryCard = null;
                }

                UpdateSummaryOutdatedFlag(mealEntries.Count);
                GenerateDailySummaryCommand.NotifyCanExecuteChanged();
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load meal entries.");
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync("Error", "Unable to load meals. Try again later.", "OK"));
        }
    }

    public async Task AddPendingEntryAsync(TrackedEntry entry)
    {
        var mealPayload = (MealPayload)entry.Payload!;
        if (string.IsNullOrWhiteSpace(entry.BlobPath))
        {
            _logger.LogWarning("AddPendingEntryAsync: Missing original blob path for entry {EntryId}.", entry.EntryId);
            return;
        }

        var displayRelativePath = mealPayload.PreviewBlobPath ?? entry.BlobPath;
        if (string.IsNullOrWhiteSpace(displayRelativePath))
        {
            _logger.LogWarning("AddPendingEntryAsync: Missing display blob path for entry {EntryId}.", entry.EntryId);
            return;
        }

        var fullPath = Path.Combine(FileSystem.AppDataDirectory, displayRelativePath);
        var originalFullPath = Path.Combine(FileSystem.AppDataDirectory, entry.BlobPath);
        var mealPhoto = new MealPhoto(
            entry.EntryId,
            fullPath,
            originalFullPath,
            mealPayload.Description ?? string.Empty,
            entry.CapturedAt,
            entry.ProcessingStatus);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Meals.Insert(0, mealPhoto);
            UpdateSummaryOutdatedFlag(Meals.Count);
        });
    }

    public async Task UpdateEntryStatusAsync(int entryId, ProcessingStatus newStatus)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var existingEntry = Meals.FirstOrDefault(m => m.EntryId == entryId);
            if (existingEntry is not null)
            {
                existingEntry.ProcessingStatus = newStatus;
                UpdateSummaryOutdatedFlag(Meals.Count);
                return;
            }

            if (SummaryCard is not null && SummaryCard.EntryId == entryId)
            {
                SummaryCard.ProcessingStatus = newStatus;

                if (newStatus == ProcessingStatus.Completed)
                {
                    SummaryCard.RefreshMetadata(SummaryCard.MealCount, DateTime.UtcNow);
                }

                if (newStatus is ProcessingStatus.Pending or ProcessingStatus.Processing)
                {
                    SummaryCard.IsOutdated = false;
                }
                else
                {
                    UpdateSummaryOutdatedFlag(Meals.Count);
                }

                GenerateDailySummaryCommand.NotifyCanExecuteChanged();
            }
        });
    }

    private void UpdateSummaryOutdatedFlag(int currentMealCount)
    {
        if (SummaryCard is null)
        {
            return;
        }

        var shouldFlag = SummaryCard.ProcessingStatus == ProcessingStatus.Completed && currentMealCount > SummaryCard.MealCount;
        SummaryCard.IsOutdated = shouldFlag;
    }

    [RelayCommand]
    private async Task RetryAnalysis(MealPhoto meal)
    {
        _logger.LogInformation("RetryAnalysis called for entry {EntryId} with status {Status}", meal.EntryId, meal.ProcessingStatus);

        if (meal.ProcessingStatus != ProcessingStatus.Failed && meal.ProcessingStatus != ProcessingStatus.Skipped)
        {
            _logger.LogWarning("RetryAnalysis called for an entry that is not in a failed or skipped state.");
            return;
        }

        _logger.LogInformation("Retrying analysis for entry {EntryId}.", meal.EntryId);

        // Update to pending status in UI
        meal.ProcessingStatus = ProcessingStatus.Pending;
        _logger.LogInformation("Status changed to Pending in UI for entry {EntryId}.", meal.EntryId);

        // Persist to database immediately so LoadEntriesAsync sees the correct state
        await _trackedEntryRepository.UpdateProcessingStatusAsync(meal.EntryId, ProcessingStatus.Pending);
        _logger.LogInformation("Status persisted to database for entry {EntryId}.", meal.EntryId);

        // Queue for processing
        await _backgroundAnalysisService.QueueEntryAsync(meal.EntryId);
        _logger.LogInformation("Analysis re-queued for entry {EntryId}.", meal.EntryId);
    }
}
