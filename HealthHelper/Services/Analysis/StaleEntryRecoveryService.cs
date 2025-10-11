using System;
using System.Linq;
using System.Threading.Tasks;
using HealthHelper.Data;
using HealthHelper.Models;
using Microsoft.Extensions.Logging;

namespace HealthHelper.Services.Analysis;

/// <summary>
/// Service to recover entries that were left in Processing state due to app shutdown
/// </summary>
public interface IStaleEntryRecoveryService
{
    Task RecoverStaleEntriesAsync();
}

public class StaleEntryRecoveryService : IStaleEntryRecoveryService
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly ILogger<StaleEntryRecoveryService> _logger;

    public StaleEntryRecoveryService(
        ITrackedEntryRepository trackedEntryRepository,
        ILogger<StaleEntryRecoveryService> logger)
    {
        _trackedEntryRepository = trackedEntryRepository;
        _logger = logger;
    }

    public async Task RecoverStaleEntriesAsync()
    {
        try
        {
            _logger.LogInformation("Checking for stale processing entries on app startup...");

            // Get today's entries to check for stale Processing states
            var entries = await _trackedEntryRepository.GetByDayAsync(DateTime.Now);

            var staleEntries = entries
                .Where(e => e.ProcessingStatus == ProcessingStatus.Processing)
                .ToList();

            if (!staleEntries.Any())
            {
                _logger.LogInformation("No stale processing entries found.");
                return;
            }

            _logger.LogWarning("Found {Count} stale processing entries. Resetting to Pending state.", staleEntries.Count);

            foreach (var entry in staleEntries)
            {
                _logger.LogInformation("Resetting entry {EntryId} from Processing to Pending.", entry.EntryId);
                await _trackedEntryRepository.UpdateProcessingStatusAsync(entry.EntryId, ProcessingStatus.Pending);
            }

            _logger.LogInformation("Stale entry recovery completed. {Count} entries reset.", staleEntries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover stale processing entries.");
        }
    }
}
