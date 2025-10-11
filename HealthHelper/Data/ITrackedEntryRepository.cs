
using System;
using HealthHelper.Models;

namespace HealthHelper.Data;

public interface ITrackedEntryRepository
{
    Task AddAsync(TrackedEntry entry);
    Task<IEnumerable<TrackedEntry>> GetByDayAsync(DateTime date, TimeZoneInfo? timeZone = null);
    Task<IEnumerable<TrackedEntry>> GetByEntryTypeAndDayAsync(string entryType, DateTime date, TimeZoneInfo? timeZone = null);
    Task DeleteAsync(int entryId);
    Task UpdateProcessingStatusAsync(int entryId, ProcessingStatus status);
    Task<TrackedEntry?> GetByIdAsync(int entryId);
    Task UpdateAsync(TrackedEntry entry);
}
