using HealthHelper.Models;
using HealthHelper.Utilities;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace HealthHelper.Data;

public class SqliteTrackedEntryRepository : ITrackedEntryRepository
{
    private readonly HealthHelperDbContext _context;

    public SqliteTrackedEntryRepository(HealthHelperDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(TrackedEntry entry)
    {
        entry.DataPayload = JsonSerializer.Serialize(entry.Payload);
        await _context.TrackedEntries.AddAsync(entry);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<TrackedEntry>> GetByDayAsync(DateTime date, TimeZoneInfo? timeZone = null)
    {
        var (utcStart, utcEnd) = DateTimeConverter.GetUtcBoundsForLocalDay(date, timeZone);

        var entries = await _context.TrackedEntries
            .AsNoTracking()  // Disable EF tracking to always get fresh data from DB
            .Where(e => e.CapturedAt >= utcStart && e.CapturedAt < utcEnd)
            .ToListAsync();

        foreach (var entry in entries)
        {
            DeserializePayload(entry);
        }
        return entries;
    }

    public async Task<IEnumerable<TrackedEntry>> GetByEntryTypeAndDayAsync(EntryType entryType, DateTime date, TimeZoneInfo? timeZone = null)
    {
        var (utcStart, utcEnd) = DateTimeConverter.GetUtcBoundsForLocalDay(date, timeZone);

        var entries = await _context.TrackedEntries
            .AsNoTracking()
            .Where(e => e.EntryType == entryType && e.CapturedAt >= utcStart && e.CapturedAt < utcEnd)
            .ToListAsync();

        foreach (var entry in entries)
        {
            DeserializePayload(entry);
        }

        return entries;
    }

    public async Task<IReadOnlyList<DaySummary>> GetDaySummariesForWeekAsync(DateTime weekStart, TimeZoneInfo? timeZone = null)
    {
        var tz = timeZone ?? TimeZoneInfo.Local;
        var localWeekStart = DateTime.SpecifyKind(weekStart.Date, DateTimeKind.Unspecified);
        var localWeekEnd = localWeekStart.AddDays(7);

        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localWeekStart, tz);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localWeekEnd, tz);

        var entries = await _context.TrackedEntries
            .AsNoTracking()
            .Where(e => e.CapturedAt >= utcStart && e.CapturedAt < utcEnd)
            .ToListAsync();

        var groupedByDate = new Dictionary<DateTime, List<TrackedEntry>>(7);

        foreach (var entry in entries)
        {
            DeserializePayload(entry);

            var localDate = DateTimeConverter.ToLocal(entry.CapturedAt, tz).Date;
            if (localDate < localWeekStart || localDate >= localWeekEnd)
            {
                continue;
            }

            if (!groupedByDate.TryGetValue(localDate, out var bucket))
            {
                bucket = new List<TrackedEntry>();
                groupedByDate[localDate] = bucket;
            }

            bucket.Add(entry);
        }

        var summaries = new List<DaySummary>(capacity: 7);

        for (var offset = 0; offset < 7; offset++)
        {
            var date = localWeekStart.AddDays(offset);
            groupedByDate.TryGetValue(date, out var dayEntries);
            summaries.Add(CreateDaySummary(date, dayEntries ?? new List<TrackedEntry>()));
        }

        return summaries;
    }

    public async Task DeleteAsync(int entryId)
    {
        var entry = await _context.TrackedEntries.FindAsync(entryId);
        if (entry is not null)
        {
            _context.TrackedEntries.Remove(entry);
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateProcessingStatusAsync(int entryId, ProcessingStatus status)
    {
        var entry = await _context.TrackedEntries.FindAsync(entryId);
        if (entry is not null)
        {
            entry.ProcessingStatus = status;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<TrackedEntry?> GetByIdAsync(int entryId)
    {
        var entry = await _context.TrackedEntries
            .AsNoTracking()  // Disable EF tracking to always get fresh data from DB
            .FirstOrDefaultAsync(e => e.EntryId == entryId);
        if (entry is not null)
        {
            DeserializePayload(entry);
        }
        return entry;
    }

    public async Task UpdateAsync(TrackedEntry entry)
    {
        var trackedEntry = await _context.TrackedEntries.FindAsync(entry.EntryId);
        if (trackedEntry is null)
        {
            return;
        }

        trackedEntry.EntryType = entry.EntryType;
        trackedEntry.CapturedAt = entry.CapturedAt;
        trackedEntry.CapturedAtTimeZoneId = entry.CapturedAtTimeZoneId;
        trackedEntry.CapturedAtOffsetMinutes = entry.CapturedAtOffsetMinutes;
        trackedEntry.BlobPath = entry.BlobPath;
        trackedEntry.DataSchemaVersion = entry.DataSchemaVersion;
        trackedEntry.DataPayload = JsonSerializer.Serialize(entry.Payload);
        trackedEntry.ProcessingStatus = entry.ProcessingStatus;
        trackedEntry.ExternalId = entry.ExternalId;

        await _context.SaveChangesAsync();
        _context.Entry(trackedEntry).State = EntityState.Detached;
    }

    public async Task UpdateEntryTypeAsync(int entryId, EntryType entryType)
    {
        var trackedEntry = await _context.TrackedEntries.FindAsync(entryId);
        if (trackedEntry is null)
        {
            return;
        }

        trackedEntry.EntryType = entryType;
        await _context.SaveChangesAsync();
        _context.Entry(trackedEntry).State = EntityState.Detached;
    }

    private void DeserializePayload(TrackedEntry entry)
    {
        if (entry.DataSchemaVersion == 0)
        {
            entry.Payload = JsonSerializer.Deserialize<PendingEntryPayload>(entry.DataPayload) ?? new PendingEntryPayload();
            return;
        }

        entry.Payload = entry.EntryType switch
        {
            EntryType.Meal => JsonSerializer.Deserialize<MealPayload>(entry.DataPayload) ?? new MealPayload(),
            EntryType.Exercise => JsonSerializer.Deserialize<ExercisePayload>(entry.DataPayload) ?? new ExercisePayload(),
            EntryType.DailySummary => NormalizeDailySummaryPayload(JsonSerializer.Deserialize<DailySummaryPayload>(entry.DataPayload)),
            _ => JsonSerializer.Deserialize<PendingEntryPayload>(entry.DataPayload) ?? new PendingEntryPayload()
        };
    }

    private static DailySummaryPayload NormalizeDailySummaryPayload(DailySummaryPayload? payload)
    {
        payload ??= new DailySummaryPayload();
        if (payload.SchemaVersion == 0)
        {
            payload.SchemaVersion = 1;
        }

        return payload;
    }

    private static DaySummary CreateDaySummary(DateTime date, IReadOnlyList<TrackedEntry> entries)
    {
        var nonSummaryEntries = entries
            .Where(e => e.EntryType != EntryType.DailySummary)
            .ToList();

        var previews = nonSummaryEntries
            .OrderByDescending(e => e.CapturedAt)
            .Select(entry => new
            {
                Entry = entry,
                Preview = ResolvePreviewPath(entry)
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.Preview))
            .Select(x => new DayPreview(x.Entry.EntryId, x.Entry.EntryType, x.Preview!))
            .Take(3)
            .ToList();

        var summaryEntry = entries
            .Where(e => e.EntryType == EntryType.DailySummary)
            .OrderByDescending(e => e.CapturedAt)
            .FirstOrDefault();

        return new DaySummary
        {
            Date = date,
            MealCount = nonSummaryEntries.Count(e => e.EntryType == EntryType.Meal),
            ExerciseCount = nonSummaryEntries.Count(e => e.EntryType == EntryType.Exercise),
            SleepCount = nonSummaryEntries.Count(e => e.EntryType == EntryType.Sleep),
            OtherCount = nonSummaryEntries.Count(e => e.EntryType == EntryType.Other),
            PendingCount = nonSummaryEntries.Count(e => e.EntryType == EntryType.Unknown),
            CompletedCount = nonSummaryEntries.Count(e => e.ProcessingStatus == ProcessingStatus.Completed),
            HasPendingOrFailedAnalysis = nonSummaryEntries.Any(e => e.ProcessingStatus is ProcessingStatus.Pending or ProcessingStatus.Processing or ProcessingStatus.Failed),
            DailySummaryStatus = summaryEntry?.ProcessingStatus,
            DailySummaryEntryId = summaryEntry?.EntryId,
            Previews = previews
        };
    }

    private static string? ResolvePreviewPath(TrackedEntry entry)
    {
        switch (entry.EntryType)
        {
            case EntryType.Meal when entry.Payload is MealPayload mealPayload:
                return mealPayload.PreviewBlobPath ?? entry.BlobPath;
            case EntryType.Exercise when entry.Payload is ExercisePayload exercisePayload:
                if (!string.IsNullOrWhiteSpace(exercisePayload.PreviewBlobPath))
                {
                    return exercisePayload.PreviewBlobPath;
                }

                if (!string.IsNullOrWhiteSpace(exercisePayload.ScreenshotBlobPath))
                {
                    return exercisePayload.ScreenshotBlobPath;
                }

                return entry.BlobPath;
            case EntryType.Sleep:
                if (entry.Payload is PendingEntryPayload sleepPayload && !string.IsNullOrWhiteSpace(sleepPayload.PreviewBlobPath))
                {
                    return sleepPayload.PreviewBlobPath;
                }

                return entry.BlobPath;
            case EntryType.Unknown when entry.Payload is PendingEntryPayload pendingPayload:
                return pendingPayload.PreviewBlobPath ?? entry.BlobPath;
            default:
                return entry.BlobPath;
        }
    }
}
