
using HealthHelper.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthHelper.Data;

public class SqliteEntryAnalysisRepository : IEntryAnalysisRepository
{
    private readonly HealthHelperDbContext _context;

    public SqliteEntryAnalysisRepository(HealthHelperDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(EntryAnalysis analysis)
    {
        await _context.EntryAnalyses.AddAsync(analysis);
        await _context.SaveChangesAsync();
    }

    public async Task<IEnumerable<EntryAnalysis>> ListByDayAsync(DateTime date)
    {
        return await _context.EntryAnalyses
            .Where(a => a.CapturedAt.Date == date.Date)
            .ToListAsync();
    }

    public async Task<EntryAnalysis?> GetByTrackedEntryIdAsync(int trackedEntryId)
    {
        return await _context.EntryAnalyses
            .FirstOrDefaultAsync(a => a.EntryId == trackedEntryId);
    }
}
