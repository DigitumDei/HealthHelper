
using HealthHelper.Models;

namespace HealthHelper.Data;

public interface ITrackedEntryRepository
{
    Task AddAsync(TrackedEntry entry);
    Task<IEnumerable<TrackedEntry>> GetByDayAsync(DateTime date);
}
