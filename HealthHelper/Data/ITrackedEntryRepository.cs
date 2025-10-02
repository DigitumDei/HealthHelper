
using HealthHelper.Models;

namespace HealthHelper.Data;

public interface ITrackedEntryRepository
{
    Task AddAsync(TrackedEntry entry);
    Task<IEnumerable<TrackedEntry>> GetByDayAsync(DateTime date);
    Task DeleteAsync(int entryId);
    Task UpdateProcessingStatusAsync(int entryId, ProcessingStatus status);
    Task<TrackedEntry?> GetByIdAsync(int entryId);
}
