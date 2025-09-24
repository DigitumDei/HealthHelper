
using HealthHelper.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthHelper.Data;

public class SqliteDailySummaryRepository : IDailySummaryRepository
{
    private readonly HealthHelperDbContext _context;

    public SqliteDailySummaryRepository(HealthHelperDbContext context)
    {
        _context = context;
    }

    public async Task AddOrUpdateAsync(DailySummary summary)
    {
        var existing = await _context.DailySummaries
            .FirstOrDefaultAsync(s => s.SummaryDate.Date == summary.SummaryDate.Date);

        if (existing is null)
        {
            await _context.DailySummaries.AddAsync(summary);
        }
        else
        {
            existing.Highlights = summary.Highlights;
            existing.Recommendations = summary.Recommendations;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<DailySummary?> GetLatestAsync()
    {
        return await _context.DailySummaries
            .OrderByDescending(s => s.SummaryDate)
            .FirstOrDefaultAsync();
    }
}
