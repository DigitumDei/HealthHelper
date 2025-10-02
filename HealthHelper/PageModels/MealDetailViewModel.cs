using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Data;
using HealthHelper.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace HealthHelper.PageModels;

[QueryProperty(nameof(Meal), "Meal")]
public partial class MealDetailViewModel : ObservableObject
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly IEntryAnalysisRepository _entryAnalysisRepository;
    private readonly ILogger<MealDetailViewModel> _logger;

    [ObservableProperty]
    private MealPhoto meal;

    [ObservableProperty]
    private string analysisText;

    public MealDetailViewModel(
        ITrackedEntryRepository trackedEntryRepository,
        IEntryAnalysisRepository entryAnalysisRepository,
        ILogger<MealDetailViewModel> logger)
    {
        _trackedEntryRepository = trackedEntryRepository;
        _entryAnalysisRepository = entryAnalysisRepository;
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
            await _trackedEntryRepository.DeleteAsync(Meal.EntryId).ConfigureAwait(false);

            if (File.Exists(Meal.FullPath))
            {
                File.Delete(Meal.FullPath);
            }

            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete meal entry {EntryId}.", Meal.EntryId);
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync("Delete failed", "We couldn't delete this meal. Try again later.", "OK"));
        }
    }

    partial void OnMealChanged(MealPhoto value)
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

    private string FormatStructuredAnalysis(EntryAnalysis analysis)
    {
        try
        {
            var result = JsonSerializer.Deserialize<MealAnalysisResult>(analysis.InsightsJson);
            if (result is null)
            {
                return analysis.InsightsJson;
            }

            var sb = new StringBuilder();

            // Check for warnings first (e.g., no food detected)
            if (result.Warnings?.Any() == true)
            {
                sb.AppendLine("ℹ️ Analysis Notes:");
                foreach (var warning in result.Warnings)
                {
                    sb.AppendLine($"  {warning}");
                }
                sb.AppendLine();
            }

            // If no food items, show a friendly message
            if (result.FoodItems?.Any() != true)
            {
                sb.AppendLine("🔍 No Food Detected");
                sb.AppendLine();
                sb.AppendLine("This image doesn't appear to contain any food items.");
                sb.AppendLine();

                // Show health insights summary if available (LLM might explain why)
                if (!string.IsNullOrEmpty(result.HealthInsights?.Summary))
                {
                    sb.AppendLine($"{result.HealthInsights.Summary}");
                    sb.AppendLine();
                }

                // If there's literally nothing, provide helpful guidance
                if (string.IsNullOrEmpty(result.HealthInsights?.Summary) &&
                    (result.Warnings == null || !result.Warnings.Any()))
                {
                    sb.AppendLine("💡 Tip: This app analyzes photos of meals and food items.");
                    sb.AppendLine("Try taking a photo of your next meal for nutritional insights!");
                }

                return sb.ToString();
            }

            // Food Items
            if (result.FoodItems?.Any() == true)
            {
                sb.AppendLine("🍽️ Food Items:");
                foreach (var item in result.FoodItems)
                {
                    sb.Append($"  • {item.Name}");
                    if (!string.IsNullOrEmpty(item.PortionSize))
                    {
                        sb.Append($" ({item.PortionSize})");
                    }
                    if (item.Calories.HasValue)
                    {
                        sb.Append($" - {item.Calories} cal");
                    }
                    sb.AppendLine();
                }
                sb.AppendLine();
            }

            // Nutrition
            if (result.Nutrition is not null && result.Nutrition.TotalCalories.HasValue)
            {
                sb.AppendLine("📊 Nutrition:");
                if (result.Nutrition.TotalCalories.HasValue)
                {
                    sb.AppendLine($"  Total Calories: {result.Nutrition.TotalCalories}");
                }
                if (result.Nutrition.Protein.HasValue)
                {
                    sb.AppendLine($"  Protein: {result.Nutrition.Protein:F1}g");
                }
                if (result.Nutrition.Carbohydrates.HasValue)
                {
                    sb.AppendLine($"  Carbs: {result.Nutrition.Carbohydrates:F1}g");
                }
                if (result.Nutrition.Fat.HasValue)
                {
                    sb.AppendLine($"  Fat: {result.Nutrition.Fat:F1}g");
                }
                if (result.Nutrition.Fiber.HasValue)
                {
                    sb.AppendLine($"  Fiber: {result.Nutrition.Fiber:F1}g");
                }
                sb.AppendLine();
            }

            // Health Insights
            if (result.HealthInsights is not null)
            {
                sb.AppendLine("💚 Health Assessment:");
                if (result.HealthInsights.HealthScore.HasValue)
                {
                    sb.AppendLine($"  Score: {result.HealthInsights.HealthScore:F1}/10");
                }
                if (!string.IsNullOrEmpty(result.HealthInsights.Summary))
                {
                    sb.AppendLine($"  {result.HealthInsights.Summary}");
                }
                sb.AppendLine();

                if (result.HealthInsights.Positives?.Any() == true)
                {
                    sb.AppendLine("  ✅ Positives:");
                    foreach (var positive in result.HealthInsights.Positives)
                    {
                        sb.AppendLine($"    • {positive}");
                    }
                    sb.AppendLine();
                }

                if (result.HealthInsights.Improvements?.Any() == true)
                {
                    sb.AppendLine("  ⚠️ Improvements:");
                    foreach (var improvement in result.HealthInsights.Improvements)
                    {
                        sb.AppendLine($"    • {improvement}");
                    }
                    sb.AppendLine();
                }

                if (result.HealthInsights.Recommendations?.Any() == true)
                {
                    sb.AppendLine("  💡 Recommendations:");
                    foreach (var recommendation in result.HealthInsights.Recommendations)
                    {
                        sb.AppendLine($"    • {recommendation}");
                    }
                }
            }

            return sb.ToString();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse structured analysis, showing raw JSON.");
            return analysis.InsightsJson;
        }
    }
}
