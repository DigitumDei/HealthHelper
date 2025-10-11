
using System;
using HealthHelper.Models;

namespace HealthHelper.Data;

public interface IEntryAnalysisRepository
{
    Task AddAsync(EntryAnalysis analysis);
    Task<IEnumerable<EntryAnalysis>> ListByDayAsync(DateTime date, TimeZoneInfo? timeZone = null);
    Task<EntryAnalysis?> GetByTrackedEntryIdAsync(int trackedEntryId);
    Task UpdateAsync(EntryAnalysis analysis);
}
