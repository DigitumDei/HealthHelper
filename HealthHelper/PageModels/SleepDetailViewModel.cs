using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Data;
using HealthHelper.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace HealthHelper.PageModels;

[QueryProperty(nameof(Sleep), "Sleep")]
public partial class SleepDetailViewModel : ObservableObject
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly IEntryAnalysisRepository _entryAnalysisRepository;
    private readonly ILogger<SleepDetailViewModel> _logger;

    [ObservableProperty]
    private SleepEntry? sleep;

    [ObservableProperty]
    private string analysisText = "No analysis available for this sleep entry.";

    public SleepDetailViewModel(
        ITrackedEntryRepository trackedEntryRepository,
        IEntryAnalysisRepository entryAnalysisRepository,
        ILogger<SleepDetailViewModel> logger)
    {
        _trackedEntryRepository = trackedEntryRepository;
        _entryAnalysisRepository = entryAnalysisRepository;
        _logger = logger;
    }

    partial void OnSleepChanged(SleepEntry? value)
    {
        _ = LoadAnalysisAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Sleep is null)
        {
            _logger.LogWarning("Delete command invoked without a selected sleep entry.");
            return;
        }

        try
        {
            _logger.LogInformation("Deleting sleep entry {EntryId}.", Sleep.EntryId);

            var pathsToDelete = await ResolveFilePathsAsync(Sleep).ConfigureAwait(false);
            foreach (var path in pathsToDelete)
            {
                TryDeleteFile(path, Sleep.EntryId);
            }

            await _trackedEntryRepository.DeleteAsync(Sleep.EntryId).ConfigureAwait(false);
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete sleep entry {EntryId}.", Sleep.EntryId);
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync("Delete failed", "We couldn't delete this sleep entry. Try again later.", "OK"));
        }
    }

    private async Task<HashSet<string>> ResolveFilePathsAsync(SleepEntry sleepEntry)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(sleepEntry.PreviewPath))
        {
            paths.Add(sleepEntry.PreviewPath);
        }

        try
        {
            var trackedEntry = await _trackedEntryRepository.GetByIdAsync(sleepEntry.EntryId).ConfigureAwait(false);
            if (trackedEntry is not null)
            {
                if (!string.IsNullOrWhiteSpace(trackedEntry.BlobPath))
                {
                    var absolute = Path.Combine(FileSystem.AppDataDirectory, trackedEntry.BlobPath);
                    paths.Add(absolute);
                }

                if (trackedEntry.Payload is PendingEntryPayload pendingPayload && !string.IsNullOrWhiteSpace(pendingPayload.PreviewBlobPath))
                {
                    var previewAbsolute = Path.Combine(FileSystem.AppDataDirectory, pendingPayload.PreviewBlobPath);
                    paths.Add(previewAbsolute);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to resolve persisted file paths for sleep entry {EntryId}.", sleepEntry.EntryId);
        }

        return paths;
    }

    private void TryDeleteFile(string path, int entryId)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogDebug("Deleted file {Path} for sleep entry {EntryId}.", path, entryId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file {Path} for sleep entry {EntryId}.", path, entryId);
        }
    }

    private async Task LoadAnalysisAsync()
    {
        if (Sleep is null)
        {
            return;
        }

        try
        {
            _logger.LogDebug("Loading sleep analysis for entry {EntryId}.", Sleep.EntryId);
            var analysis = await _entryAnalysisRepository.GetByTrackedEntryIdAsync(Sleep.EntryId).ConfigureAwait(false);
            if (analysis is null)
            {
                AnalysisText = "No analysis available for this sleep entry.";
                return;
            }

            AnalysisText = FormatAnalysis(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load sleep analysis for entry {EntryId}.", Sleep.EntryId);
            AnalysisText = "We couldn't load the analysis for this sleep entry.";
        }
    }

    private static string FormatAnalysis(EntryAnalysis analysis)
    {
        try
        {
            var unified = JsonSerializer.Deserialize<UnifiedAnalysisResult>(analysis.InsightsJson);
            if (unified is null)
            {
                return analysis.InsightsJson;
            }

            if (!string.Equals(unified.EntryType, "Sleep", StringComparison.OrdinalIgnoreCase) || unified.SleepAnalysis is null)
            {
                return "This entry was classified as a different type. Sleep-specific analysis is not available.";
            }

            var sleep = unified.SleepAnalysis;
            var builder = new StringBuilder();

            if (unified.Confidence > 0)
            {
                builder.AppendLine($"Confidence: {(unified.Confidence * 100):0.#}%");
                builder.AppendLine();
            }

            if (sleep.DurationHours is double duration)
            {
                builder.AppendLine($"Duration: {duration:0.#} hours");
            }

            if (sleep.SleepScore is double score)
            {
                builder.AppendLine($"Sleep Score: {score:0.#}/100");
            }

            if (!string.IsNullOrWhiteSpace(sleep.QualitySummary))
            {
                builder.AppendLine();
                builder.AppendLine(sleep.QualitySummary);
            }

            if (sleep.EnvironmentNotes is { Count: > 0 })
            {
                builder.AppendLine();
                builder.AppendLine("Environment notes:");
                foreach (var note in sleep.EnvironmentNotes)
                {
                    builder.AppendLine($"• {note}");
                }
            }

            if (sleep.Recommendations is { Count: > 0 })
            {
                builder.AppendLine();
                builder.AppendLine("Recommendations:");
                foreach (var recommendation in sleep.Recommendations)
                {
                    builder.AppendLine($"• {recommendation}");
                }
            }

            var content = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(content)
                ? "No additional sleep insights were provided."
                : content;
        }
        catch (JsonException)
        {
            return analysis.InsightsJson;
        }
    }
}
