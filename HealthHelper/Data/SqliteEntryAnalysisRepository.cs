
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
        _context.Entry(analysis).State = EntityState.Detached;
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
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.EntryId == trackedEntryId);
    }

    public async Task UpdateAsync(EntryAnalysis analysis)
    {
        var tracked = await _context.EntryAnalyses
            .FindAsync(analysis.AnalysisId)
            .ConfigureAwait(false);

        if (tracked is null)
        {
            _context.EntryAnalyses.Attach(analysis);
            _context.Entry(analysis).State = EntityState.Modified;
        }
        else
        {
            _context.Entry(tracked).CurrentValues.SetValues(analysis);
        }

        await _context.SaveChangesAsync().ConfigureAwait(false);

        if (tracked is not null)
        {
            _context.Entry(tracked).State = EntityState.Detached;
        }
        else
        {
            _context.Entry(analysis).State = EntityState.Detached;
        }
    }
}
