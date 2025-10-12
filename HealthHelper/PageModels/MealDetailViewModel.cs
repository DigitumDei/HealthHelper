using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Services.Analysis;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace HealthHelper.PageModels;

[QueryProperty(nameof(Meal), "Meal")]
public partial class MealDetailViewModel : ObservableObject
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly IEntryAnalysisRepository _entryAnalysisRepository;
    private readonly IAnalysisOrchestrator _analysisOrchestrator;
    private readonly ILogger<MealDetailViewModel> _logger;

    [ObservableProperty]
    private MealPhoto? meal;

    [ObservableProperty]
    private string analysisText = string.Empty;

    [ObservableProperty]
    private bool isCorrectionMode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCorrectionCommand))]
    private string correctionText = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCorrectionCommand))]
    private bool isSubmittingCorrection;

    public MealDetailViewModel(
        ITrackedEntryRepository trackedEntryRepository,
        IEntryAnalysisRepository entryAnalysisRepository,
        IAnalysisOrchestrator analysisOrchestrator,
        ILogger<MealDetailViewModel> logger)
    {
        _trackedEntryRepository = trackedEntryRepository;
        _entryAnalysisRepository = entryAnalysisRepository;
        _analysisOrchestrator = analysisOrchestrator;
        _logger = logger;
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (Meal is null)
        {
            _logger.LogWarning("Delete command invoked without a selected meal.");
            return;
        }

        try
        {
            _logger.LogInformation("Deleting meal entry {EntryId}.", Meal.EntryId);

            var pathsToDelete = await ResolveFilePathsAsync(Meal).ConfigureAwait(false);

            foreach (var path in pathsToDelete)
            {
                TryDeleteFile(path, Meal.EntryId);
            }

            await _trackedEntryRepository.DeleteAsync(Meal.EntryId).ConfigureAwait(false);

            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete meal entry {EntryId}.", Meal.EntryId);
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync("Delete failed", "We couldn't delete this meal. Try again later.", "OK"));
        }
    }

    private async Task<HashSet<string>> ResolveFilePathsAsync(MealPhoto meal)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(meal.OriginalPath))
        {
            paths.Add(meal.OriginalPath);
        }

        if (!string.IsNullOrWhiteSpace(meal.FullPath))
        {
            paths.Add(meal.FullPath);
        }

        try
        {
            var trackedEntry = await _trackedEntryRepository.GetByIdAsync(meal.EntryId).ConfigureAwait(false);
            if (trackedEntry is not null)
            {
                if (!string.IsNullOrWhiteSpace(trackedEntry.BlobPath))
                {
                    var originalAbsolutePath = Path.Combine(FileSystem.AppDataDirectory, trackedEntry.BlobPath);
                    paths.Add(originalAbsolutePath);
                }

                if (trackedEntry.Payload is MealPayload trackedPayload && !string.IsNullOrWhiteSpace(trackedPayload.PreviewBlobPath))
                {
                    var previewAbsolutePath = Path.Combine(FileSystem.AppDataDirectory, trackedPayload.PreviewBlobPath);
                    paths.Add(previewAbsolutePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to resolve persisted file paths for entry {EntryId} during deletion.", meal.EntryId);
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
                _logger.LogDebug("Deleted file {Path} for entry {EntryId}.", path, entryId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete file {Path} for entry {EntryId}.", path, entryId);
        }
    }

    partial void OnMealChanged(MealPhoto? value)
    {
        _ = LoadAnalysisAsync();
    }

    private async Task LoadAnalysisAsync()
    {
        if (Meal is null)
        {
            return;
        }
        try
        {
            _logger.LogDebug("Loading analysis for entry {EntryId}.", Meal.EntryId);
            var analysis = await _entryAnalysisRepository.GetByTrackedEntryIdAsync(Meal.EntryId).ConfigureAwait(false);
            if (analysis is not null)
            {
                AnalysisText = FormatStructuredAnalysis(analysis);
            }
            else
            {
                AnalysisText = "No analysis available for this entry.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load analysis for entry {EntryId}.", Meal.EntryId);
            AnalysisText = "We couldn't load the analysis for this meal.";
        }
    }

    public string CorrectionToggleButtonText => IsCorrectionMode ? "Cancel correction" : "Update analysis";

    [RelayCommand]
    private void ToggleCorrection()
    {
        if (IsSubmittingCorrection)
        {
            return;
        }

        IsCorrectionMode = !IsCorrectionMode;
    }

    [RelayCommand(CanExecute = nameof(CanSubmitCorrection))]
    private async Task SubmitCorrectionAsync()
    {
        if (Meal is null)
        {
            _logger.LogWarning("SubmitCorrection invoked without a selected meal.");
            return;
        }

        var trimmedCorrection = CorrectionText?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedCorrection))
        {
            return;
        }

        try
        {
            IsSubmittingCorrection = true;
            _logger.LogInformation("Submitting correction for entry {EntryId}.", Meal.EntryId);

            var trackedEntry = await _trackedEntryRepository.GetByIdAsync(Meal.EntryId).ConfigureAwait(false);
            if (trackedEntry is null)
            {
                _logger.LogWarning("Tracked entry {EntryId} not found when submitting correction.", Meal.EntryId);
                await ShowAlertOnMainThreadAsync("Update failed", "We couldn't find this meal entry anymore. Try refreshing.");
                return;
            }

            var existingAnalysis = await _entryAnalysisRepository.GetByTrackedEntryIdAsync(Meal.EntryId).ConfigureAwait(false);
            if (existingAnalysis is null)
            {
                _logger.LogWarning("No analysis exists for entry {EntryId}; cannot apply correction.", Meal.EntryId);
                await ShowAlertOnMainThreadAsync("No analysis yet", "We need an existing analysis before you can submit corrections.");
                return;
            }

            var result = await _analysisOrchestrator
                .ProcessCorrectionAsync(trackedEntry, existingAnalysis, trimmedCorrection!)
                .ConfigureAwait(false);

            if (result.IsQueued)
            {
                _logger.LogInformation("Successfully applied correction for entry {EntryId}.", Meal.EntryId);
                await LoadAnalysisAsync().ConfigureAwait(false);
                await ShowAlertOnMainThreadAsync("Analysis updated", "Thanks! We've updated the analysis with your notes.");
                IsCorrectionMode = false;
                CorrectionText = string.Empty;
            }
            else
            {
                var message = result.UserMessage ?? "We couldn't update the analysis. Try again later.";
                await ShowAlertOnMainThreadAsync("Update failed", message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit correction for entry {EntryId}.", Meal.EntryId);
            await ShowAlertOnMainThreadAsync("Update failed", "We couldn't update this analysis. Try again later.");
        }
        finally
        {
            IsSubmittingCorrection = false;
        }
    }

    private bool CanSubmitCorrection()
    {
        return !IsSubmittingCorrection && !string.IsNullOrWhiteSpace(CorrectionText);
    }

    private static async Task ShowAlertOnMainThreadAsync(string title, string message)
    {
        await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync(title, message, "OK"));
    }

    private string FormatStructuredAnalysis(EntryAnalysis analysis)
    {
        try
        {
            var unified = JsonSerializer.Deserialize<UnifiedAnalysisResult>(analysis.InsightsJson);
            if (unified is null)
            {
                return analysis.InsightsJson;
            }

            if (!string.Equals(unified.EntryType, "Meal", StringComparison.OrdinalIgnoreCase) || unified.MealAnalysis is null)
            {
                return "This entry was classified as a different type. Meal-specific analysis is not available.";
            }

            return FormatMealAnalysis(unified.MealAnalysis, unified.Warnings, unified.Confidence);
        }
        catch (JsonException)
        {
            return analysis.InsightsJson;
        }
    }

    private static string FormatValue(double? value, string suffix)
    {
        return value.HasValue ? $"{value:0.##} {suffix}" : "unknown";
    }

    private string FormatMealAnalysis(MealAnalysisResult result, IEnumerable<string>? unifiedWarnings, double confidence)
    {
        var sb = new StringBuilder();

        var combinedWarnings = new List<string>();
        if (unifiedWarnings is not null)
        {
            combinedWarnings.AddRange(unifiedWarnings.Where(w => !string.IsNullOrWhiteSpace(w)));
        }
        if (result.Warnings?.Any() == true)
        {
            combinedWarnings.AddRange(result.Warnings.Where(w => !string.IsNullOrWhiteSpace(w)));
        }

        if (combinedWarnings.Any())
        {
            sb.AppendLine("ℹ️ Analysis Notes:");
            foreach (var warning in combinedWarnings)
            {
                sb.AppendLine($"  {warning}");
            }
            sb.AppendLine();
        }

        if (confidence > 0)
        {
            sb.AppendLine($"Confidence: {(confidence * 100):0.#}%");
            sb.AppendLine();
        }

        if (result.FoodItems?.Any() != true)
        {
            sb.AppendLine("🔍 No Food Detected");
            sb.AppendLine();
            sb.AppendLine("This image doesn't appear to contain any food items.");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(result.HealthInsights?.Summary))
            {
                sb.AppendLine(result.HealthInsights.Summary);
                sb.AppendLine();
            }

            if (string.IsNullOrEmpty(result.HealthInsights?.Summary) && !combinedWarnings.Any())
            {
                sb.AppendLine("💡 Tip: This app analyzes photos of meals. Try capturing a photo of your food for nutritional insights!");
            }

            return sb.ToString();
        }

        if (result.FoodItems is not null)
        {
            sb.AppendLine("🍽️ Food Items:");
            foreach (var item in result.FoodItems)
            {
                var caloriesText = item.Calories.HasValue ? $"{item.Calories} kcal" : "calories unknown";
                var portionText = item.PortionSize ?? "portion unknown";
                sb.AppendLine($"• {item.Name} ({portionText}) - {caloriesText} (confidence {(item.Confidence * 100):0.#}%)");
            }
            sb.AppendLine();
        }

        if (result.Nutrition is not null)
        {
            sb.AppendLine("⚖️ Nutrition Estimate:");
            sb.AppendLine($"• Calories: {FormatValue(result.Nutrition.TotalCalories, "kcal")}");
            sb.AppendLine($"• Protein: {FormatValue(result.Nutrition.Protein, "g")}");
            sb.AppendLine($"• Carbs: {FormatValue(result.Nutrition.Carbohydrates, "g")}");
            sb.AppendLine($"• Fat: {FormatValue(result.Nutrition.Fat, "g")}");
            sb.AppendLine($"• Fiber: {FormatValue(result.Nutrition.Fiber, "g")}");
            sb.AppendLine($"• Sugar: {FormatValue(result.Nutrition.Sugar, "g")}");
            sb.AppendLine($"• Sodium: {FormatValue(result.Nutrition.Sodium, "mg")}");
            sb.AppendLine();
        }

        if (result.HealthInsights is not null)
        {
            sb.AppendLine("🩺 Health Insights:");
            if (result.HealthInsights.HealthScore.HasValue)
            {
                sb.AppendLine($"• Health Score: {result.HealthInsights.HealthScore:0.0}/10");
            }
            if (!string.IsNullOrWhiteSpace(result.HealthInsights.Summary))
            {
                sb.AppendLine($"• Summary: {result.HealthInsights.Summary}");
            }

            if (result.HealthInsights.Positives?.Any() == true)
            {
                sb.AppendLine("• Positives:");
                foreach (var positive in result.HealthInsights.Positives)
                {
                    sb.AppendLine($"   - {positive}");
                }
            }

            if (result.HealthInsights.Improvements?.Any() == true)
            {
                sb.AppendLine("• Improvements:");
                foreach (var improvement in result.HealthInsights.Improvements)
                {
                    sb.AppendLine($"   - {improvement}");
                }
            }

            if (result.HealthInsights.Recommendations?.Any() == true)
            {
                sb.AppendLine("• Recommendations:");
                foreach (var recommendation in result.HealthInsights.Recommendations)
                {
                    sb.AppendLine($"   - {recommendation}");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }
    partial void OnIsCorrectionModeChanged(bool value)
    {
        OnPropertyChanged(nameof(CorrectionToggleButtonText));

        if (!value)
        {
            CorrectionText = string.Empty;
        }
    }
}
