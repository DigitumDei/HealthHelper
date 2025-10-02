
using HealthHelper.Models;
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

    public async Task<IEnumerable<TrackedEntry>> GetByDayAsync(DateTime date)
    {
        var entries = await _context.TrackedEntries
            .AsNoTracking()  // Disable EF tracking to always get fresh data from DB
            .Where(e => e.CapturedAt.Date == date.Date)
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

    private void DeserializePayload(TrackedEntry entry)
    {
        // A more robust implementation would use the EntryType to deserialize to the correct type
        if (entry.EntryType == "Meal")
        {
            entry.Payload = JsonSerializer.Deserialize<MealPayload>(entry.DataPayload) ?? new MealPayload();
        }
    }
}
