using System.IO;
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
            EntryType = EntryType.Unknown,
            BlobPath = "Entries/Unknown/photo.jpg",
            DataSchemaVersion = 0,
            Payload = new PendingEntryPayload
            {
                Description = "shared meal",
                PreviewBlobPath = "Entries/Unknown/photo_preview.jpg"
            }
        };

        var repository = new RecordingRepository();

        await UnifiedAnalysisApplier.ApplyAsync(
            entry,
            detectedEntryType: EntryType.Meal,
            repository,
            NullLogger.Instance);

        Assert.Equal(EntryType.Meal, entry.EntryType);
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
            EntryType = EntryType.Unknown,
            BlobPath = "Entries/Unknown/run.jpg",
            DataSchemaVersion = 0,
            Payload = new PendingEntryPayload
            {
                Description = "run summary",
                PreviewBlobPath = "Entries/Unknown/run_preview.jpg"
            }
        };

        var repository = new RecordingRepository();

        await UnifiedAnalysisApplier.ApplyAsync(
            entry,
            detectedEntryType: EntryType.Exercise,
            repository,
            NullLogger.Instance);

        var exercisePayload = Assert.IsType<ExercisePayload>(entry.Payload);
        Assert.Equal("run summary", exercisePayload.Description);
        Assert.Equal("Entries/Unknown/run_preview.jpg", exercisePayload.PreviewBlobPath);
        Assert.Equal("Entries/Unknown/run.jpg", exercisePayload.ScreenshotBlobPath);
        Assert.Equal(EntryType.Exercise, entry.EntryType);
        Assert.Equal(1, entry.DataSchemaVersion);
        Assert.Equal(1, repository.UpdateCallCount);
    }

    [Fact]
    public async Task ApplyAsync_WithSleepClassification_PreservesPendingPayload()
    {
        var entry = new TrackedEntry
        {
            EntryId = 100,
            EntryType = EntryType.Unknown,
            BlobPath = "Entries/Unknown/sleep.jpg",
            DataSchemaVersion = 0,
            Payload = new PendingEntryPayload
            {
                Description = "sleep tracking",
                PreviewBlobPath = "Entries/Unknown/sleep_preview.jpg"
            }
        };

        var repository = new RecordingRepository();

        await UnifiedAnalysisApplier.ApplyAsync(
            entry,
            detectedEntryType: EntryType.Sleep,
            repository,
            NullLogger.Instance);

        Assert.Equal(EntryType.Sleep, entry.EntryType);
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
            EntryType = EntryType.Meal,
            BlobPath = "Entries/Meal/photo.jpg",
            DataSchemaVersion = 1,
            Payload = new MealPayload
            {
                Description = "existing meal",
                PreviewBlobPath = "Entries/Meal/photo_preview.jpg"
            }
        };

        var repository = new RecordingRepository();

        await UnifiedAnalysisApplier.ApplyAsync(
            entry,
            detectedEntryType: EntryType.Meal,
            repository,
            NullLogger.Instance);

        Assert.Equal(0, repository.UpdateCallCount);
        Assert.Equal(EntryType.Meal, entry.EntryType);
        Assert.Equal(1, entry.DataSchemaVersion);
    }

    [Fact]
    public async Task ApplyAsync_TypeChange_MovesAssetsIntoTypedDirectory()
    {
        var testId = Guid.NewGuid().ToString("N");
        var photoFileName = $"{testId}.jpg";
        var previewFileName = $"{testId}_preview.jpg";

        var originalRelative = Path.Combine("Entries", "Unknown", photoFileName);
        var previewRelative = Path.Combine("Entries", "Unknown", previewFileName);

        var baseDirectory = AppContext.BaseDirectory;
        var unknownDirectory = Path.Combine(baseDirectory, "Entries", "Unknown");
        Directory.CreateDirectory(unknownDirectory);

        var photoAbsolute = Path.Combine(baseDirectory, originalRelative);
        var previewAbsolute = Path.Combine(baseDirectory, previewRelative);

        await File.WriteAllTextAsync(photoAbsolute, "photo");
        await File.WriteAllTextAsync(previewAbsolute, "preview");

        var entry = new TrackedEntry
        {
            EntryId = 201,
            EntryType = EntryType.Unknown,
            BlobPath = originalRelative,
            DataSchemaVersion = 0,
            Payload = new PendingEntryPayload
            {
                Description = "pending",
                PreviewBlobPath = previewRelative
            }
        };

        var repository = new RecordingRepository();

        try
        {
            await UnifiedAnalysisApplier.ApplyAsync(
                entry,
                detectedEntryType: EntryType.Meal,
                repository,
                NullLogger.Instance);

            Assert.Equal(EntryType.Meal, entry.EntryType);
            Assert.Equal(1, entry.DataSchemaVersion);

            var mealPayload = Assert.IsType<MealPayload>(entry.Payload);
            Assert.NotNull(entry.BlobPath);
            Assert.NotNull(mealPayload.PreviewBlobPath);

            var movedPhotoAbsolute = Path.Combine(baseDirectory, entry.BlobPath!);
            var movedPreviewAbsolute = Path.Combine(baseDirectory, mealPayload.PreviewBlobPath!);

            Assert.True(File.Exists(movedPhotoAbsolute));
            Assert.True(File.Exists(movedPreviewAbsolute));
            Assert.Contains(Path.Combine("Entries", "Meal"), entry.BlobPath!, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Path.Combine("Entries", "Meal"), mealPayload.PreviewBlobPath!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteFile(photoAbsolute);
            TryDeleteFile(previewAbsolute);

            if (entry.BlobPath is not null)
            {
                TryDeleteFile(Path.Combine(baseDirectory, entry.BlobPath));
            }

            if (entry.Payload is MealPayload payload)
            {
                TryDeleteFile(Path.Combine(baseDirectory, payload.PreviewBlobPath!));
            }

            CleanupEmptyDirectories(Path.Combine(baseDirectory, "Entries"));
        }
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
        public Task<IEnumerable<TrackedEntry>> GetByEntryTypeAndDayAsync(EntryType entryType, DateTime date, TimeZoneInfo? timeZone = null) => throw new NotImplementedException();
        public Task<TrackedEntry?> GetByIdAsync(int entryId) => throw new NotImplementedException();
        public Task UpdateEntryTypeAsync(int entryId, EntryType entryType) => throw new NotImplementedException();
        public Task UpdateProcessingStatusAsync(int entryId, ProcessingStatus status) => throw new NotImplementedException();
        #endregion
    }

    private static void TryDeleteFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup failures in tests.
        }
    }

    private static void CleanupEmptyDirectories(string root)
    {
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var directory in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            if (Directory.Exists(directory) && Directory.GetFileSystemEntries(directory).Length == 0)
            {
                try
                {
                    Directory.Delete(directory, recursive: false);
                }
                catch
                {
                    // best-effort cleanup
                }
            }
        }

        if (Directory.Exists(root) && Directory.GetFileSystemEntries(root).Length == 0)
        {
            try
            {
                Directory.Delete(root, recursive: false);
            }
            catch
            {
                // ignore
            }
        }
    }
}
