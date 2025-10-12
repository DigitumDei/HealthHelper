using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Services.Analysis;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace HealthHelper.Tests.Services.Analysis;

public class UnifiedAnalysisApplierTests
{
    [Fact]
    public async Task ApplyAsync_WithPendingMealPayload_ConvertsAndPersists()
    {
        var entry = new TrackedEntry
        {
            EntryId = 42,
            EntryType = "Unknown",
            BlobPath = "Entries/Unknown/photo.jpg",
            DataSchemaVersion = 0,
            Payload = new PendingEntryPayload
            {
                Description = "shared meal",
                PreviewBlobPath = "Entries/Unknown/photo_preview.jpg"
            }
        };

        var unified = new UnifiedAnalysisResult
        {
            EntryType = "Meal"
        };

        var repository = new RecordingRepository();

        await UnifiedAnalysisApplier.ApplyAsync(
            entry,
            detectedEntryType: "Meal",
            repository,
            NullLogger.Instance);

        Assert.Equal("Meal", entry.EntryType);
        Assert.Equal(1, entry.DataSchemaVersion);
        var mealPayload = Assert.IsType<MealPayload>(entry.Payload);
        Assert.Equal("shared meal", mealPayload.Description);
        Assert.Equal("Entries/Unknown/photo_preview.jpg", mealPayload.PreviewBlobPath);
        Assert.Equal(1, repository.UpdateCallCount);
        Assert.Same(entry, repository.LastUpdatedEntry);
    }

    [Fact]
    public async Task ApplyAsync_WithPendingExercisePayload_UsesScreenshotBlobPath()
    {
        var entry = new TrackedEntry
        {
            EntryId = 7,
            EntryType = "Unknown",
            BlobPath = "Entries/Unknown/run.jpg",
            DataSchemaVersion = 0,
            Payload = new PendingEntryPayload
            {
                Description = "run summary",
                PreviewBlobPath = "Entries/Unknown/run_preview.jpg"
            }
        };

        var unified = new UnifiedAnalysisResult
        {
            EntryType = "Exercise"
        };

        var repository = new RecordingRepository();

        await UnifiedAnalysisApplier.ApplyAsync(
            entry,
            detectedEntryType: "Exercise",
            repository,
            NullLogger.Instance);

        var exercisePayload = Assert.IsType<ExercisePayload>(entry.Payload);
        Assert.Equal("run summary", exercisePayload.Description);
        Assert.Equal("Entries/Unknown/run_preview.jpg", exercisePayload.PreviewBlobPath);
        Assert.Equal("Entries/Unknown/run.jpg", exercisePayload.ScreenshotBlobPath);
        Assert.Equal("Exercise", entry.EntryType);
        Assert.Equal(1, entry.DataSchemaVersion);
        Assert.Equal(1, repository.UpdateCallCount);
    }

    [Fact]
    public async Task ApplyAsync_WithSleepClassification_PreservesPendingPayload()
    {
        var entry = new TrackedEntry
        {
            EntryId = 100,
            EntryType = "Unknown",
            BlobPath = "Entries/Unknown/sleep.jpg",
            DataSchemaVersion = 0,
            Payload = new PendingEntryPayload
            {
                Description = "sleep tracking",
                PreviewBlobPath = "Entries/Unknown/sleep_preview.jpg"
            }
        };

        var unified = new UnifiedAnalysisResult
        {
            EntryType = "Sleep"
        };

        var repository = new RecordingRepository();

        await UnifiedAnalysisApplier.ApplyAsync(
            entry,
            detectedEntryType: "Sleep",
            repository,
            NullLogger.Instance);

        Assert.Equal("Sleep", entry.EntryType);
        Assert.Equal(0, entry.DataSchemaVersion);
        var pendingPayload = Assert.IsType<PendingEntryPayload>(entry.Payload);
        Assert.Equal("sleep tracking", pendingPayload.Description);
        Assert.Equal(1, repository.UpdateCallCount);
    }

    [Fact]
    public async Task ApplyAsync_NoChanges_DoesNotPersist()
    {
        var entry = new TrackedEntry
        {
            EntryId = 55,
            EntryType = "Meal",
            BlobPath = "Entries/Meal/photo.jpg",
            DataSchemaVersion = 1,
            Payload = new MealPayload
            {
                Description = "existing meal",
                PreviewBlobPath = "Entries/Meal/photo_preview.jpg"
            }
        };

        var unified = new UnifiedAnalysisResult
        {
            EntryType = "Meal"
        };

        var repository = new RecordingRepository();

        await UnifiedAnalysisApplier.ApplyAsync(
            entry,
            detectedEntryType: "Meal",
            repository,
            NullLogger.Instance);

        Assert.Equal(0, repository.UpdateCallCount);
        Assert.Equal("Meal", entry.EntryType);
        Assert.Equal(1, entry.DataSchemaVersion);
    }

    private sealed class RecordingRepository : ITrackedEntryRepository
    {
        public int UpdateCallCount { get; private set; }
        public TrackedEntry? LastUpdatedEntry { get; private set; }

        public Task UpdateAsync(TrackedEntry entry)
        {
            UpdateCallCount++;
            LastUpdatedEntry = entry;
            return Task.CompletedTask;
        }

        #region Unused interface members
        public Task AddAsync(TrackedEntry entry) => throw new NotImplementedException();
        public Task DeleteAsync(int entryId) => throw new NotImplementedException();
        public Task<IEnumerable<TrackedEntry>> GetByDayAsync(DateTime date, TimeZoneInfo? timeZone = null) => throw new NotImplementedException();
        public Task<IEnumerable<TrackedEntry>> GetByEntryTypeAndDayAsync(string entryType, DateTime date, TimeZoneInfo? timeZone = null) => throw new NotImplementedException();
        public Task<TrackedEntry?> GetByIdAsync(int entryId) => throw new NotImplementedException();
        public Task UpdateEntryTypeAsync(int entryId, string entryType) => throw new NotImplementedException();
        public Task UpdateProcessingStatusAsync(int entryId, ProcessingStatus status) => throw new NotImplementedException();
        #endregion
    }
}
