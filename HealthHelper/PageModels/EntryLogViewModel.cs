using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Services.Analysis;
using HealthHelper.Utilities;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;

namespace HealthHelper.PageModels;

public partial class EntryLogViewModel : ObservableObject
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly IBackgroundAnalysisService _backgroundAnalysisService;
    private readonly ILogger<EntryLogViewModel> _logger;
    private readonly SemaphoreSlim _summaryCardLock = new(1, 1);
    public ObservableCollection<TrackedEntryCard> Entries { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateDailySummaryCommand))]
    private DailySummaryCard? summaryCard;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GenerateDailySummaryCommand))]
    private bool isGeneratingSummary;

    public bool ShowGenerateSummaryButton => SummaryCard is null;
    public bool ShowSummaryCard => SummaryCard is not null;

    public EntryLogViewModel(ITrackedEntryRepository trackedEntryRepository, IBackgroundAnalysisService backgroundAnalysisService, ILogger<EntryLogViewModel> logger)
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
    private async Task GoToEntryDetail(TrackedEntryCard entry)
    {
        if (entry is null)
        {
            _logger.LogWarning("Attempted to navigate to entry details with a null reference.");
            return;
        }

        if (!entry.IsClickable)
        {
            _logger.LogInformation("Entry {EntryId} is not yet ready for viewing.", entry.EntryId);
            await Shell.Current.DisplayAlertAsync(
                "Still Processing",
                "This entry is still being analyzed. Please wait a moment.",
                "OK");
            return;
        }

        try
        {
            if (entry is MealPhoto meal)
            {
                _logger.LogInformation("Navigating to meal detail for entry {EntryId}.", meal.EntryId);
                await Shell.Current.GoToAsync(nameof(MealDetailPage),
                    new Dictionary<string, object>
                    {
                        { "Meal", meal }
                    });
                return;
            }

            if (entry is ExerciseEntry exercise)
            {
                _logger.LogInformation("Navigating to exercise detail for entry {EntryId}.", exercise.EntryId);
                await Shell.Current.GoToAsync(nameof(ExerciseDetailPage),
                    new Dictionary<string, object>
                    {
                        { "Exercise", exercise }
                    });
                return;
            }

            _logger.LogWarning("No detail page registered for entry type {EntryType}.", entry.EntryType);
            await Shell.Current.DisplayAlertAsync("Unsupported entry", "This entry type cannot be opened yet.", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to entry detail for entry {EntryId}.", entry.EntryId);
            await Shell.Current.DisplayAlertAsync("Navigation error", "Unable to open entry details right now.", "OK");
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

            var entriesForDay = await _trackedEntryRepository
                .GetByDayAsync(DateTime.Now)
                .ConfigureAwait(false);

            var entryCountSnapshot = entriesForDay
                .Where(entry => entry.EntryType != EntryType.DailySummary)
                .Count(entry => entry.ProcessingStatus == ProcessingStatus.Completed);
            var summaryCapturedAtUtc = DateTime.UtcNow;
            var (summaryTimeZoneId, summaryOffsetMinutes) = DateTimeConverter.CaptureTimeZoneMetadata(summaryCapturedAtUtc);

            var summaryPayload = new DailySummaryPayload
            {
                SchemaVersion = 1,
                EntryCount = entryCountSnapshot,
                GeneratedAt = summaryCapturedAtUtc,
                GeneratedAtTimeZoneId = summaryTimeZoneId,
                GeneratedAtOffsetMinutes = summaryOffsetMinutes
            };

            var existingSummaryEntries = await _trackedEntryRepository
                .GetByEntryTypeAndDayAsync(EntryType.DailySummary, DateTime.Now)
                .ConfigureAwait(false);

            var existingSummary = existingSummaryEntries
                .OrderByDescending(entry => entry.CapturedAt)
                .FirstOrDefault();

            TrackedEntry summaryEntry;

            if (existingSummary is null)
            {
                summaryEntry = new TrackedEntry
                {
                    EntryType = EntryType.DailySummary,
                    CapturedAt = summaryCapturedAtUtc,
                    CapturedAtTimeZoneId = summaryTimeZoneId,
                    CapturedAtOffsetMinutes = summaryOffsetMinutes,
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
                existingSummary.CapturedAt = summaryCapturedAtUtc;
                existingSummary.CapturedAtTimeZoneId = summaryTimeZoneId;
                existingSummary.CapturedAtOffsetMinutes = summaryOffsetMinutes;
                existingSummary.ProcessingStatus = ProcessingStatus.Pending;
                existingSummary.DataSchemaVersion = summaryPayload.SchemaVersion;

                await _trackedEntryRepository.UpdateAsync(existingSummary).ConfigureAwait(false);
                summaryEntry = existingSummary;
            }

            var hasExplicitGeneratedAt = summaryPayload.GeneratedAt != default;
            var effectiveGeneratedAt = hasExplicitGeneratedAt
                ? summaryPayload.GeneratedAt
                : summaryEntry.CapturedAt;
            var effectiveGeneratedAtTimeZoneId = hasExplicitGeneratedAt
                ? summaryPayload.GeneratedAtTimeZoneId ?? summaryEntry.CapturedAtTimeZoneId
                : summaryEntry.CapturedAtTimeZoneId;
            var effectiveGeneratedAtOffsetMinutes = hasExplicitGeneratedAt
                ? summaryPayload.GeneratedAtOffsetMinutes ?? summaryEntry.CapturedAtOffsetMinutes
                : summaryEntry.CapturedAtOffsetMinutes;

            var generatedAtTimeZone = DateTimeConverter.ResolveTimeZone(
                effectiveGeneratedAtTimeZoneId,
                effectiveGeneratedAtOffsetMinutes ?? summaryEntry.CapturedAtOffsetMinutes);
            if (effectiveGeneratedAtOffsetMinutes is null && generatedAtTimeZone is not null)
            {
                effectiveGeneratedAtOffsetMinutes = DateTimeConverter.GetUtcOffsetMinutes(generatedAtTimeZone, effectiveGeneratedAt);
            }

            await WithSummaryCardLockAsync(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    SummaryCard ??= new DailySummaryCard(
                        summaryEntry.EntryId,
                        summaryPayload.EntryCount,
                        effectiveGeneratedAt,
                        effectiveGeneratedAtTimeZoneId,
                        effectiveGeneratedAtOffsetMinutes,
                        summaryEntry.ProcessingStatus);
                    SummaryCard.RefreshMetadata(
                        summaryPayload.EntryCount,
                        effectiveGeneratedAt,
                        effectiveGeneratedAtTimeZoneId,
                        effectiveGeneratedAtOffsetMinutes);
                    SummaryCard.ProcessingStatus = ProcessingStatus.Pending;
                    SummaryCard.IsOutdated = false;
                    GenerateDailySummaryCommand.NotifyCanExecuteChanged();
                });
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
            _logger.LogDebug("Loading tracked entries for {Date}.", DateTime.Now.Date);
            var entries = (await _trackedEntryRepository.GetByDayAsync(DateTime.Now).ConfigureAwait(false))
                .ToList();

            var stuckEntries = entries
                .Where(entry => entry.ProcessingStatus == ProcessingStatus.Pending)
                .ToList();

            foreach (var stuck in stuckEntries)
            {
                try
                {
                    stuck.ProcessingStatus = ProcessingStatus.Failed;
                    await _trackedEntryRepository.UpdateProcessingStatusAsync(stuck.EntryId, ProcessingStatus.Failed).ConfigureAwait(false);
                }
                catch (Exception updateEx)
                {
                    _logger.LogWarning(updateEx, "Failed to downgrade processing entry {EntryId} to Failed state after reload.", stuck.EntryId);
                    stuck.ProcessingStatus = ProcessingStatus.Failed;
                }
            }

            var summaryEntries = await _trackedEntryRepository
                .GetByEntryTypeAndDayAsync(EntryType.DailySummary, DateTime.Now)
                .ConfigureAwait(false);

            var summaryEntry = summaryEntries
                .OrderByDescending(entry => entry.CapturedAt)
                .FirstOrDefault();

            DailySummaryCard? summaryCard = null;
            if (summaryEntry is not null)
            {
                var payload = summaryEntry.Payload as DailySummaryPayload ?? new DailySummaryPayload();
                var hasExplicitGeneratedAt = payload.GeneratedAt != default;
                var generatedAt = hasExplicitGeneratedAt
                    ? payload.GeneratedAt
                    : summaryEntry.CapturedAt;
                var generatedAtTimeZoneId = hasExplicitGeneratedAt
                    ? payload.GeneratedAtTimeZoneId ?? summaryEntry.CapturedAtTimeZoneId
                    : summaryEntry.CapturedAtTimeZoneId;
                var generatedAtOffsetMinutes = hasExplicitGeneratedAt
                    ? payload.GeneratedAtOffsetMinutes ?? summaryEntry.CapturedAtOffsetMinutes
                    : summaryEntry.CapturedAtOffsetMinutes;

                var generatedAtTimeZone = DateTimeConverter.ResolveTimeZone(
                    generatedAtTimeZoneId,
                    generatedAtOffsetMinutes ?? summaryEntry.CapturedAtOffsetMinutes);
                if (generatedAtOffsetMinutes is null && generatedAtTimeZone is not null)
                {
                    generatedAtOffsetMinutes = DateTimeConverter.GetUtcOffsetMinutes(generatedAtTimeZone, generatedAt);
                }

                summaryCard = new DailySummaryCard(
                    summaryEntry.EntryId,
                    payload.EntryCount,
                    generatedAt,
                    generatedAtTimeZoneId,
                    generatedAtOffsetMinutes,
                    summaryEntry.ProcessingStatus);
            }

            var mealEntries = entries
                .Where(entry => entry.EntryType == EntryType.Meal)
                .ToList();

            var pendingEntries = entries
                .Where(entry => entry.EntryType == EntryType.Unknown && entry.Payload is PendingEntryPayload)
                .ToList();

            var pendingCards = pendingEntries
                .Select(entry =>
                {
                    if (string.IsNullOrWhiteSpace(entry.BlobPath))
                    {
                        _logger.LogWarning("Skipping pending entry {EntryId} because original blob path is missing.", entry.EntryId);
                        return null;
                    }

                    if (entry.Payload is not PendingEntryPayload pendingPayload)
                    {
                        return null;
                    }

                    var displayRelativePath = pendingPayload.PreviewBlobPath ?? entry.BlobPath;
                    if (string.IsNullOrWhiteSpace(displayRelativePath))
                    {
                        _logger.LogWarning("Skipping pending entry {EntryId} because preview path is missing.", entry.EntryId);
                        return null;
                    }

                    var displayFullPath = Path.Combine(FileSystem.AppDataDirectory, displayRelativePath);
                    var originalFullPath = Path.Combine(FileSystem.AppDataDirectory, entry.BlobPath);
                    return new MealPhoto(
                        entry.EntryId,
                        displayFullPath,
                        originalFullPath,
                        pendingPayload.Description ?? string.Empty,
                        entry.CapturedAt,
                        entry.CapturedAtTimeZoneId,
                        entry.CapturedAtOffsetMinutes,
                        entry.ProcessingStatus,
                        entryType: EntryType.Unknown);
                })
                .OfType<MealPhoto>();

            var mealCards = mealEntries
                .Select(entry =>
                {
                    if (string.IsNullOrWhiteSpace(entry.BlobPath) || entry.Payload is not MealPayload mealPayload)
                    {
                        _logger.LogWarning("Skipping entry {EntryId} because file paths or payload are missing.", entry.EntryId);
                        return null;
                    }

                    var displayPathRelative = mealPayload.PreviewBlobPath ?? entry.BlobPath;
                    if (string.IsNullOrWhiteSpace(displayPathRelative))
                    {
                        _logger.LogWarning("Skipping entry {EntryId} because preview path is missing.", entry.EntryId);
                        return null;
                    }

                    var displayFullPath = Path.Combine(FileSystem.AppDataDirectory, displayPathRelative);
                    var originalFullPath = Path.Combine(FileSystem.AppDataDirectory, entry.BlobPath);
                    return new MealPhoto(
                        entry.EntryId,
                        displayFullPath,
                        originalFullPath,
                        mealPayload.Description ?? string.Empty,
                        entry.CapturedAt,
                        entry.CapturedAtTimeZoneId,
                        entry.CapturedAtOffsetMinutes,
                        entry.ProcessingStatus);
                })
                .OfType<MealPhoto>();

            var exerciseCards = entries
                .Where(entry => entry.EntryType == EntryType.Exercise && entry.Payload is ExercisePayload)
                .Select(entry =>
                {
                    var exercisePayload = (ExercisePayload)entry.Payload!;
                    var previewRelativePath = exercisePayload.PreviewBlobPath ?? exercisePayload.ScreenshotBlobPath ?? entry.BlobPath;
                    var screenshotRelativePath = exercisePayload.ScreenshotBlobPath ?? entry.BlobPath;

                    if (string.IsNullOrWhiteSpace(previewRelativePath) || string.IsNullOrWhiteSpace(screenshotRelativePath))
                    {
                        _logger.LogWarning("Skipping exercise entry {EntryId} because file paths are missing.", entry.EntryId);
                        return null;
                    }

                    var previewFullPath = Path.Combine(FileSystem.AppDataDirectory, previewRelativePath);
                    var screenshotFullPath = Path.Combine(FileSystem.AppDataDirectory, screenshotRelativePath);

                    return new ExerciseEntry(
                        entry.EntryId,
                        previewFullPath,
                        screenshotFullPath,
                        exercisePayload.Description,
                        exercisePayload.ExerciseType,
                        entry.CapturedAt,
                        entry.CapturedAtTimeZoneId,
                        entry.CapturedAtOffsetMinutes,
                        entry.ProcessingStatus);
                })
                .OfType<ExerciseEntry>();

            var combinedCards = pendingCards
                .Cast<TrackedEntryCard>()
                .Concat(mealCards)
                .Concat(exerciseCards)
                .OrderByDescending(card => card.CapturedAtUtc)
                .ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Entries.Clear();
                foreach (var card in combinedCards)
                {
                    Entries.Add(card);
                }
            });

            await WithSummaryCardLockAsync(async () =>
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (summaryCard is not null)
                    {
                        var isNewCard = SummaryCard is null || SummaryCard.EntryId != summaryCard.EntryId;
                        if (isNewCard)
                        {
                            SummaryCard = summaryCard;
                        }
                        else if (SummaryCard is not null)
                        {
                            SummaryCard.RefreshMetadata(
                                summaryCard.EntryCount,
                                summaryCard.GeneratedAt,
                                summaryCard.GeneratedAtTimeZoneId,
                                summaryCard.GeneratedAtOffsetMinutes);
                            SummaryCard.ProcessingStatus = summaryCard.ProcessingStatus;
                        }
                    }
                    else
                    {
                        SummaryCard = null;
                    }

                    UpdateSummaryOutdatedFlag();
                    GenerateDailySummaryCommand.NotifyCanExecuteChanged();
                });
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load daily entries.");
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync("Error", "Unable to load entries. Try again later.", "OK"));
        }
    }

    private TrackedEntryCard? CreateCardFromEntry(TrackedEntry entry)
    {
        if (entry is null)
        {
            return null;
        }

        if (entry.EntryType == EntryType.Exercise && entry.Payload is ExercisePayload exercisePayload)
        {
            var previewRelativePath = exercisePayload.PreviewBlobPath ?? exercisePayload.ScreenshotBlobPath ?? entry.BlobPath;
            var screenshotRelativePath = exercisePayload.ScreenshotBlobPath ?? entry.BlobPath;

            if (string.IsNullOrWhiteSpace(previewRelativePath) || string.IsNullOrWhiteSpace(screenshotRelativePath))
            {
                _logger.LogWarning("CreateCardFromEntry: Missing preview or screenshot path for exercise entry {EntryId}.", entry.EntryId);
                return null;
            }

            var previewFullPath = Path.Combine(FileSystem.AppDataDirectory, previewRelativePath);
            var screenshotFullPath = Path.Combine(FileSystem.AppDataDirectory, screenshotRelativePath);

            return new ExerciseEntry(
                entry.EntryId,
                previewFullPath,
                screenshotFullPath,
                exercisePayload.Description,
                exercisePayload.ExerciseType,
                entry.CapturedAt,
                entry.CapturedAtTimeZoneId,
                entry.CapturedAtOffsetMinutes,
                entry.ProcessingStatus);
        }

        if (entry.Payload is MealPayload mealPayload && !string.IsNullOrWhiteSpace(entry.BlobPath))
        {
            var displayRelativePath = mealPayload.PreviewBlobPath ?? entry.BlobPath;
            if (string.IsNullOrWhiteSpace(displayRelativePath))
            {
                _logger.LogWarning("CreateCardFromEntry: Missing display blob path for meal entry {EntryId}.", entry.EntryId);
                return null;
            }

            var fullPath = Path.Combine(FileSystem.AppDataDirectory, displayRelativePath);
            var originalFullPath = Path.Combine(FileSystem.AppDataDirectory, entry.BlobPath);
            return new MealPhoto(
                entry.EntryId,
                fullPath,
                originalFullPath,
                mealPayload.Description ?? string.Empty,
                entry.CapturedAt,
                entry.CapturedAtTimeZoneId,
                entry.CapturedAtOffsetMinutes,
                entry.ProcessingStatus);
        }

        if (entry.Payload is PendingEntryPayload pendingPayload && !string.IsNullOrWhiteSpace(entry.BlobPath))
        {
            var displayRelativePath = pendingPayload.PreviewBlobPath ?? entry.BlobPath;
            if (string.IsNullOrWhiteSpace(displayRelativePath))
            {
                _logger.LogWarning("CreateCardFromEntry: Missing preview blob path for pending entry {EntryId}.", entry.EntryId);
                return null;
            }

            var fullPath = Path.Combine(FileSystem.AppDataDirectory, displayRelativePath);
            var originalFullPath = Path.Combine(FileSystem.AppDataDirectory, entry.BlobPath);
            return new MealPhoto(
                entry.EntryId,
                fullPath,
                originalFullPath,
                pendingPayload.Description ?? string.Empty,
                entry.CapturedAt,
                entry.CapturedAtTimeZoneId,
                entry.CapturedAtOffsetMinutes,
                entry.ProcessingStatus,
                entryType: EntryType.Unknown);
        }

        return null;
    }

    public async Task AddPendingEntryAsync(TrackedEntry entry)
    {
        if (entry is null)
        {
            return;
        }

        var card = CreateCardFromEntry(entry);
        if (card is null)
        {
            _logger.LogWarning("AddPendingEntryAsync: Unsupported entry payload type {PayloadType} for entry {EntryId}.", entry.Payload?.GetType().Name ?? "null", entry.EntryId);
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() => Entries.Insert(0, card));

        await WithSummaryCardLockAsync(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(UpdateSummaryOutdatedFlag);
        });
    }

    public async Task UpdateEntryStatusAsync(int entryId, ProcessingStatus newStatus)
    {
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var existingEntry = Entries.FirstOrDefault(card => card.EntryId == entryId);
            if (existingEntry is not null)
            {
                existingEntry.ProcessingStatus = newStatus;
            }
        });

        await WithSummaryCardLockAsync(async () =>
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (SummaryCard is null || SummaryCard.EntryId != entryId)
                {
                    UpdateSummaryOutdatedFlag();
                    return;
                }

                SummaryCard.ProcessingStatus = newStatus;

                UpdateSummaryOutdatedFlag();
                GenerateDailySummaryCommand.NotifyCanExecuteChanged();
            });
        });

        if (newStatus == ProcessingStatus.Completed)
        {
            await LoadEntriesAsync().ConfigureAwait(false);
        }
    }

    private void UpdateSummaryOutdatedFlag()
    {
        if (SummaryCard is null)
        {
            return;
        }

        var currentCompletedEntryCount = Entries
            .Where(card => card.EntryType != EntryType.DailySummary)
            .Count(card => card.ProcessingStatus == ProcessingStatus.Completed);

        if (SummaryCard.ProcessingStatus == ProcessingStatus.Completed)
        {
            SummaryCard.IsOutdated = currentCompletedEntryCount > SummaryCard.EntryCount;
            return;
        }

        SummaryCard.IsOutdated = SummaryCard.ProcessingStatus is ProcessingStatus.Failed or ProcessingStatus.Skipped;
    }

    private async Task WithSummaryCardLockAsync(Func<Task> action)
    {
        await _summaryCardLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await action().ConfigureAwait(false);
        }
        finally
        {
            _summaryCardLock.Release();
        }
    }

    [RelayCommand]
    private async Task RetryAnalysis(TrackedEntryCard entry)
    {
        _logger.LogInformation("RetryAnalysis called for entry {EntryId} with status {Status}", entry.EntryId, entry.ProcessingStatus);

        if (entry.ProcessingStatus != ProcessingStatus.Failed && entry.ProcessingStatus != ProcessingStatus.Skipped)
        {
            _logger.LogWarning("RetryAnalysis called for an entry that is not in a failed or skipped state.");
            return;
        }

        _logger.LogInformation("Retrying analysis for entry {EntryId}.", entry.EntryId);

        entry.ProcessingStatus = ProcessingStatus.Pending;
        _logger.LogInformation("Status changed to Pending in UI for entry {EntryId}.", entry.EntryId);

        await _trackedEntryRepository.UpdateProcessingStatusAsync(entry.EntryId, ProcessingStatus.Pending);
        _logger.LogInformation("Status persisted to database for entry {EntryId}.", entry.EntryId);

        await _backgroundAnalysisService.QueueEntryAsync(entry.EntryId);
        _logger.LogInformation("Analysis re-queued for entry {EntryId}.", entry.EntryId);
    }
}
