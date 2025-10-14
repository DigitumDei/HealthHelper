using System.Linq;
using System.Text.Json;
using HealthHelper.Data;
using HealthHelper.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HealthHelper.Tests.Data;

public sealed class SqliteTrackedEntryRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly HealthHelperDbContext _context;
    private readonly SqliteTrackedEntryRepository _repository;

    public SqliteTrackedEntryRepositoryTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<HealthHelperDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new HealthHelperDbContext(options);
        _context.Database.EnsureCreated();

        _repository = new SqliteTrackedEntryRepository(_context);
    }

    [Fact]
    public async Task GetDaySummariesForWeekAsync_ProducesSevenDaysWithCountsAndPreviews()
    {
        var weekStart = new DateTime(2024, 10, 7);

        var mondayMealPayload = new MealPayload
        {
            Description = "Lunch bowl",
            PreviewBlobPath = "Entries/Meal/meal_preview.jpg"
        };

        var mondayMeal = new TrackedEntry
        {
            EntryType = EntryType.Meal,
            CapturedAt = new DateTime(2024, 10, 7, 12, 0, 0, DateTimeKind.Utc),
            BlobPath = "Entries/Meal/meal_original.jpg",
            DataSchemaVersion = 1,
            DataPayload = JsonSerializer.Serialize(mondayMealPayload),
            ProcessingStatus = ProcessingStatus.Completed,
            Payload = mondayMealPayload
        };

        var tuesdaySummaryPayload = new DailySummaryPayload
        {
            EntryCount = 3,
            SchemaVersion = 1
        };

        var tuesdaySummary = new TrackedEntry
        {
            EntryType = EntryType.DailySummary,
            CapturedAt = new DateTime(2024, 10, 8, 22, 0, 0, DateTimeKind.Utc),
            DataSchemaVersion = 1,
            DataPayload = JsonSerializer.Serialize(tuesdaySummaryPayload),
            ProcessingStatus = ProcessingStatus.Completed,
            Payload = tuesdaySummaryPayload
        };

        var wednesdayPendingPayload = new PendingEntryPayload
        {
            Description = "Awaiting classification",
            PreviewBlobPath = "Entries/Unknown/pending_preview.jpg"
        };

        var wednesdayPending = new TrackedEntry
        {
            EntryType = EntryType.Unknown,
            CapturedAt = new DateTime(2024, 10, 9, 9, 30, 0, DateTimeKind.Utc),
            BlobPath = "Entries/Unknown/pending_original.jpg",
            DataSchemaVersion = 0,
            DataPayload = JsonSerializer.Serialize(wednesdayPendingPayload),
            ProcessingStatus = ProcessingStatus.Pending,
            Payload = wednesdayPendingPayload
        };

        var outsideEntry = new TrackedEntry
        {
            EntryType = EntryType.Sleep,
            CapturedAt = new DateTime(2024, 10, 14, 1, 0, 0, DateTimeKind.Utc),
            BlobPath = "Entries/Sleep/outside.jpg",
            DataSchemaVersion = 0,
            DataPayload = JsonSerializer.Serialize(new PendingEntryPayload { PreviewBlobPath = "Entries/Sleep/outside_preview.jpg" }),
            ProcessingStatus = ProcessingStatus.Completed,
            Payload = new PendingEntryPayload { PreviewBlobPath = "Entries/Sleep/outside_preview.jpg" }
        };

        _context.TrackedEntries.AddRange(mondayMeal, tuesdaySummary, wednesdayPending, outsideEntry);
        await _context.SaveChangesAsync();

        var results = await _repository.GetDaySummariesForWeekAsync(weekStart, TimeZoneInfo.Utc);

        Assert.Equal(7, results.Count);

        var monday = results[0];
        Assert.Equal(weekStart.Date, monday.Date);
        Assert.Equal(1, monday.MealCount);
        Assert.Equal(1, monday.CompletedCount);
        Assert.Equal(EntryType.Meal, monday.Previews.First().EntryType);
        Assert.Single(monday.Previews);
        Assert.Equal("Entries/Meal/meal_preview.jpg", monday.Previews.First().RelativePath);

        var tuesday = results[1];
        Assert.Equal(ProcessingStatus.Completed, tuesday.DailySummaryStatus);
        Assert.True(tuesday.DailySummaryEntryId.HasValue);
        Assert.Equal(tuesdaySummary.EntryId, tuesday.DailySummaryEntryId);

        var wednesday = results[2];
        Assert.Equal(1, wednesday.PendingCount);
        Assert.True(wednesday.HasPendingOrFailedAnalysis);

        var sunday = results[6];
        Assert.Equal(0, sunday.TotalCount);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
