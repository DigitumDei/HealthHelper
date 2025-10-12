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

[QueryProperty(nameof(Exercise), "Exercise")]
public partial class ExerciseDetailViewModel : ObservableObject
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly IEntryAnalysisRepository _entryAnalysisRepository;
    private readonly ILogger<ExerciseDetailViewModel> _logger;

    [ObservableProperty]
    private ExerciseEntry? exercise;

    [ObservableProperty]
    private string analysisText = "No analysis available for this exercise.";

    public ExerciseDetailViewModel(
        ITrackedEntryRepository trackedEntryRepository,
        IEntryAnalysisRepository entryAnalysisRepository,
        ILogger<ExerciseDetailViewModel> logger)
    {
        _trackedEntryRepository = trackedEntryRepository;
        _entryAnalysisRepository = entryAnalysisRepository;
        _logger = logger;
    }

    partial void OnExerciseChanged(ExerciseEntry? value)
    {
        _ = LoadAnalysisAsync();
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (Exercise is null)
        {
            _logger.LogWarning("Delete command invoked without a selected exercise.");
            return;
        }

        try
        {
            _logger.LogInformation("Deleting exercise entry {EntryId}.", Exercise.EntryId);

            var pathsToDelete = await ResolveFilePathsAsync(Exercise).ConfigureAwait(false);
            foreach (var path in pathsToDelete)
            {
                TryDeleteFile(path, Exercise.EntryId);
            }

            await _trackedEntryRepository.DeleteAsync(Exercise.EntryId).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete exercise entry {EntryId}.", Exercise.EntryId);
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync("Delete failed", "We couldn't delete this exercise. Try again later.", "OK"));
        }
    }

    private async Task<HashSet<string>> ResolveFilePathsAsync(ExerciseEntry exercise)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(exercise.PreviewPath))
        {
            paths.Add(exercise.PreviewPath);
        }

        if (!string.IsNullOrWhiteSpace(exercise.ScreenshotPath))
        {
            paths.Add(exercise.ScreenshotPath);
        }

        try
        {
            var trackedEntry = await _trackedEntryRepository.GetByIdAsync(exercise.EntryId).ConfigureAwait(false);
            if (trackedEntry is not null)
            {
                if (!string.IsNullOrWhiteSpace(trackedEntry.BlobPath))
                {
                    var absolute = Path.Combine(FileSystem.AppDataDirectory, trackedEntry.BlobPath);
                    paths.Add(absolute);
                }

                if (trackedEntry.Payload is ExercisePayload payload)
                {
                    if (!string.IsNullOrWhiteSpace(payload.PreviewBlobPath))
                    {
                        var previewAbsolute = Path.Combine(FileSystem.AppDataDirectory, payload.PreviewBlobPath);
                        paths.Add(previewAbsolute);
                    }

                    if (!string.IsNullOrWhiteSpace(payload.ScreenshotBlobPath))
                    {
                        var screenshotAbsolute = Path.Combine(FileSystem.AppDataDirectory, payload.ScreenshotBlobPath);
                        paths.Add(screenshotAbsolute);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to resolve persisted file paths for exercise entry {EntryId}.", exercise.EntryId);
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
                _logger.LogDebug("Deleted file {Path} for exercise entry {EntryId}.", path, entryId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file {Path} for exercise entry {EntryId}.", path, entryId);
        }
    }

    private async Task LoadAnalysisAsync()
    {
        if (Exercise is null)
        {
            return;
        }

        try
        {
            _logger.LogDebug("Loading exercise analysis for entry {EntryId}.", Exercise.EntryId);
            var analysis = await _entryAnalysisRepository.GetByTrackedEntryIdAsync(Exercise.EntryId).ConfigureAwait(false);
            if (analysis is null)
            {
                AnalysisText = "No analysis available for this exercise.";
                return;
            }

            AnalysisText = FormatAnalysis(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load exercise analysis for entry {EntryId}.", Exercise.EntryId);
            AnalysisText = "We couldn't load the analysis for this exercise.";
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

            if (!string.Equals(unified.EntryType, "Exercise", StringComparison.OrdinalIgnoreCase) || unified.ExerciseAnalysis is null)
            {
                return "This entry was classified as a different type. Exercise-specific analysis is not available.";
            }

            var result = unified.ExerciseAnalysis;
            var builder = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(result.ActivityType))
            {
                builder.AppendLine($"Activity: {result.ActivityType}");
            }

            if (unified.Confidence > 0)
            {
                builder.AppendLine($"Confidence: {(unified.Confidence * 100):0.#}%");
                builder.AppendLine();
            }

            if (result.Metrics is not null)
            {
                AppendMetric(builder, "Distance", result.Metrics.Distance, result.Metrics.DistanceUnit);
                AppendMetric(builder, "Duration (minutes)", result.Metrics.DurationMinutes, null);
                AppendMetric(builder, "Average pace", result.Metrics.AveragePace, null);
                AppendMetric(builder, "Average speed", result.Metrics.AverageSpeed, result.Metrics.SpeedUnit);
                AppendMetric(builder, "Calories", result.Metrics.Calories, null);
                AppendMetric(builder, "Avg heart rate", result.Metrics.AverageHeartRate, "bpm");
                AppendMetric(builder, "Max heart rate", result.Metrics.MaxHeartRate, "bpm");
                AppendMetric(builder, "Steps", result.Metrics.Steps.HasValue ? (double?)result.Metrics.Steps.Value : null, null);
                AppendMetric(builder, "Elevation gain", result.Metrics.ElevationGain, result.Metrics.ElevationUnit);
            }

            if (result.Insights is not null)
            {
                if (!string.IsNullOrWhiteSpace(result.Insights.Summary))
                {
                    builder.AppendLine();
                    builder.AppendLine(result.Insights.Summary);
                }

                if (result.Insights.Positives.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("Positives:");
                    foreach (var positive in result.Insights.Positives)
                    {
                        builder.AppendLine($" - {positive}");
                    }
                }

                if (result.Insights.Improvements.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("Improvements:");
                    foreach (var improvement in result.Insights.Improvements)
                    {
                        builder.AppendLine($" - {improvement}");
                    }
                }

                if (result.Insights.Recommendations.Count > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine("Recommendations:");
                    foreach (var recommendation in result.Insights.Recommendations)
                    {
                        builder.AppendLine($" - {recommendation}");
                    }
                }
            }

            if (unified.Warnings.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("Warnings:");
                foreach (var warning in unified.Warnings)
                {
                    builder.AppendLine($" - {warning}");
                }
            }

            return builder.ToString();
        }
        catch (JsonException)
        {
            return analysis.InsightsJson;
        }
    }
    private static void AppendMetric(StringBuilder builder, string label, double? value, string? unit)
    {
        if (value is null)
        {
            return;
        }

        var formattedValue = value.Value.ToString("0.##", CultureInfo.InvariantCulture);

        if (!string.IsNullOrWhiteSpace(unit))
        {
            builder.AppendLine($"{label}: {formattedValue} {unit}");
        }
        else
        {
            builder.AppendLine($"{label}: {formattedValue}");
        }
    }

    private static void AppendMetric(StringBuilder builder, string label, string? value, string? unit)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var suffix = string.IsNullOrWhiteSpace(unit) ? string.Empty : $" {unit}";
        builder.AppendLine($"{label}: {value}{suffix}");
    }
}
