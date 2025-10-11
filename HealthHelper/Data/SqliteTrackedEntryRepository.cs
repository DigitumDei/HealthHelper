
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

    public async Task<IEnumerable<TrackedEntry>> GetByEntryTypeAndDayAsync(string entryType, DateTime date, TimeZoneInfo? timeZone = null)
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

    private void DeserializePayload(TrackedEntry entry)
    {
        entry.Payload = entry.EntryType switch
        {
            "Meal" => JsonSerializer.Deserialize<MealPayload>(entry.DataPayload) ?? new MealPayload(),
            "DailySummary" => NormalizeDailySummaryPayload(JsonSerializer.Deserialize<DailySummaryPayload>(entry.DataPayload)),
            _ => entry.Payload
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
}
