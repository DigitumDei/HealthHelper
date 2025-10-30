using System;
using System.Threading;
using System.Threading.Tasks;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Services.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HealthHelper.Services.Analysis;

public interface IBackgroundAnalysisService
{
    /// <summary>
    /// Queue an entry for background analysis
    /// </summary>
    Task QueueEntryAsync(int entryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Queue a correction analysis for an entry (re-analysis with user feedback)
    /// </summary>
    Task QueueCorrectionAsync(int entryId, string correction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when an entry's processing status changes
    /// </summary>
    event EventHandler<EntryStatusChangedEventArgs>? StatusChanged;
}

public class BackgroundAnalysisService : IBackgroundAnalysisService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BackgroundAnalysisService> _logger;
    private readonly IBackgroundExecutionService _backgroundExecution;

    public event EventHandler<EntryStatusChangedEventArgs>? StatusChanged;

    public BackgroundAnalysisService(
        IServiceScopeFactory scopeFactory,
        ILogger<BackgroundAnalysisService> logger,
        IBackgroundExecutionService backgroundExecution)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _backgroundExecution = backgroundExecution;
    }

    public Task QueueEntryAsync(int entryId, CancellationToken cancellationToken = default)
    {
        _ = Task.Run(async () =>
        {
            var taskName = $"analyze-{entryId}";
            _backgroundExecution.StartBackgroundTask(taskName);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var entryRepository = scope.ServiceProvider.GetRequiredService<ITrackedEntryRepository>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IAnalysisOrchestrator>();

                try
                {
                    // Check cancellation before starting
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Analysis cancelled before starting for entry {EntryId}.", entryId);
                        return;
                    }

                    await UpdateStatusAsync(entryRepository, entryId, ProcessingStatus.Processing);

                    var entry = await entryRepository.GetByIdAsync(entryId);
                    if (entry is null)
                    {
                        _logger.LogWarning("Entry {EntryId} not found for analysis.", entryId);
                        return;
                    }

                    // Check cancellation before expensive LLM call
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Analysis cancelled before LLM call for entry {EntryId}.", entryId);
                        await UpdateStatusAsync(entryRepository, entryId, ProcessingStatus.Pending);
                        return;
                    }

                    var result = await orchestrator.ProcessEntryAsync(entry, cancellationToken).ConfigureAwait(false);

                    // Check cancellation after LLM call
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Analysis cancelled after LLM call for entry {EntryId}.", entryId);
                        await UpdateStatusAsync(entryRepository, entryId, ProcessingStatus.Pending);
                        return;
                    }

                    var finalStatus = result.IsQueued
                        ? ProcessingStatus.Completed
                        : (result.RequiresCredentials
                            ? ProcessingStatus.Skipped
                            : ProcessingStatus.Failed);

                    await UpdateStatusAsync(entryRepository, entryId, finalStatus);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Background analysis was cancelled for entry {EntryId}.", entryId);
                    await UpdateStatusAsync(entryRepository, entryId, ProcessingStatus.Pending);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background analysis failed for entry {EntryId}.", entryId);
                    await UpdateStatusAsync(entryRepository, entryId, ProcessingStatus.Failed);
                }
            }
            finally
            {
                _backgroundExecution.StopBackgroundTask(taskName);
            }
        }, cancellationToken);
        return Task.CompletedTask;
    }

    public Task QueueCorrectionAsync(int entryId, string correction, CancellationToken cancellationToken = default)
    {
        _ = Task.Run(async () =>
        {
            var taskName = $"correct-{entryId}";
            _backgroundExecution.StartBackgroundTask(taskName);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var entryRepository = scope.ServiceProvider.GetRequiredService<ITrackedEntryRepository>();
                var analysisRepository = scope.ServiceProvider.GetRequiredService<IEntryAnalysisRepository>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IAnalysisOrchestrator>();

                try
                {
                    // Check cancellation before starting
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Correction analysis cancelled before starting for entry {EntryId}.", entryId);
                        return;
                    }

                    await UpdateStatusAsync(entryRepository, entryId, ProcessingStatus.Processing);

                    var entry = await entryRepository.GetByIdAsync(entryId);
                    if (entry is null)
                    {
                        _logger.LogWarning("Entry {EntryId} not found for correction analysis.", entryId);
                        return;
                    }

                    var existingAnalysis = await analysisRepository.GetByTrackedEntryIdAsync(entryId);
                    if (existingAnalysis is null)
                    {
                        _logger.LogWarning("No existing analysis found for entry {EntryId}. Cannot apply correction.", entryId);
                        await UpdateStatusAsync(entryRepository, entryId, ProcessingStatus.Failed);
                        return;
                    }

                    // Check cancellation before expensive LLM call
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Correction analysis cancelled before LLM call for entry {EntryId}.", entryId);
                        await UpdateStatusAsync(entryRepository, entryId, ProcessingStatus.Pending);
                        return;
                    }

                    var result = await orchestrator.ProcessCorrectionAsync(entry, existingAnalysis, correction, cancellationToken).ConfigureAwait(false);

                    // Check cancellation after LLM call
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogInformation("Correction analysis cancelled after LLM call for entry {EntryId}.", entryId);
                        await UpdateStatusAsync(entryRepository, entryId, ProcessingStatus.Pending);
                        return;
                    }

                    var finalStatus = result.IsQueued
                        ? ProcessingStatus.Completed
                        : (result.RequiresCredentials
                            ? ProcessingStatus.Skipped
                            : ProcessingStatus.Failed);

                    await UpdateStatusAsync(entryRepository, entryId, finalStatus);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Correction analysis was cancelled for entry {EntryId}.", entryId);
                    await UpdateStatusAsync(entryRepository, entryId, ProcessingStatus.Pending);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Correction analysis failed for entry {EntryId}.", entryId);
                    await UpdateStatusAsync(entryRepository, entryId, ProcessingStatus.Failed);
                }
            }
            finally
            {
                _backgroundExecution.StopBackgroundTask(taskName);
            }
        }, cancellationToken);
        return Task.CompletedTask;
    }

    private async Task UpdateStatusAsync(ITrackedEntryRepository entryRepository, int entryId, ProcessingStatus status)
    {
        await entryRepository.UpdateProcessingStatusAsync(entryId, status);
        StatusChanged?.Invoke(this, new EntryStatusChangedEventArgs(entryId, status));
    }
}
