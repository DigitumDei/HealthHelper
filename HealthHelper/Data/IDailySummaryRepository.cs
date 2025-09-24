
using HealthHelper.Models;

namespace HealthHelper.Data;

public interface IDailySummaryRepository
{
    Task AddOrUpdateAsync(DailySummary summary);
    Task<DailySummary?> GetLatestAsync();
}
