using HealthHelper.Data;
using HealthHelper.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HealthHelper.Services.Analysis;

public interface IBackgroundAnalysisService
{
    /// <summary>
    /// Queue an entry for background analysis
    /// </summary>
    Task QueueEntryAsync(int entryId);

    /// <summary>
    /// Event raised when an entry's processing status changes
    /// </summary>
    event EventHandler<EntryStatusChangedEventArgs>? StatusChanged;
}

public class BackgroundAnalysisService : IBackgroundAnalysisService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundAnalysisService> _logger;

    public event EventHandler<EntryStatusChangedEventArgs>? StatusChanged;

    public BackgroundAnalysisService(IServiceScopeFactory scopeFactory, ILogger<BackgroundAnalysisService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public Task QueueEntryAsync(int entryId)
    {
        _ = Task.Run(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            var entryRepository = scope.ServiceProvider.GetRequiredService<ITrackedEntryRepository>();
            var orchestrator = scope.ServiceProvider.GetRequiredService<IAnalysisOrchestrator>();

            try
            {
                await UpdateStatusAsync(entryRepository, entryId, ProcessingStatus.Processing);

                var entry = await entryRepository.GetByIdAsync(entryId);
                if (entry is null)
                {
                    _logger.LogWarning("Entry {EntryId} not found for analysis.", entryId);
                    return;
                }

                var result = await orchestrator.ProcessEntryAsync(entry);

                var finalStatus = result.IsQueued
                    ? ProcessingStatus.Completed
                    : (result.RequiresCredentials
                        ? ProcessingStatus.Skipped
                        : ProcessingStatus.Failed);

                await UpdateStatusAsync(entryRepository, entryId, finalStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background analysis failed for entry {EntryId}.", entryId);
                await UpdateStatusAsync(entryRepository, entryId, ProcessingStatus.Failed);
            }
        });
        return Task.CompletedTask;
    }

    private async Task UpdateStatusAsync(ITrackedEntryRepository entryRepository, int entryId, ProcessingStatus status)
    {
        await entryRepository.UpdateProcessingStatusAsync(entryId, status);
        StatusChanged?.Invoke(this, new EntryStatusChangedEventArgs(entryId, status));
    }
}
