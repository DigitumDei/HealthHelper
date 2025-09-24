
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
            .Where(e => e.CapturedAt.Date == date.Date)
            .ToListAsync();

        foreach (var entry in entries)
        {
            // A more robust implementation would use the EntryType to deserialize to the correct type
            if (entry.EntryType == "Meal")
            {
                entry.Payload = JsonSerializer.Deserialize<MealPayload>(entry.DataPayload) ?? new MealPayload();
            }
        }

        return entries;
    }
}
