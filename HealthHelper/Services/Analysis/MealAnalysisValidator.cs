using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using HealthHelper.Models;
using Microsoft.Extensions.Logging;

namespace HealthHelper.Services.Analysis;

/// <summary>
/// Validates meal analysis results for schema compliance and data quality.
/// </summary>
public class MealAnalysisValidator
{
    private readonly ILogger<MealAnalysisValidator> _logger;

    public MealAnalysisValidator(ILogger<MealAnalysisValidator> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Validates a meal analysis result and returns any validation errors.
    /// </summary>
    public ValidationResult Validate(string insightsJson, string expectedSchemaVersion = "1.0")
    {
        var result = new ValidationResult { IsValid = true };

        try
        {
            var analysis = JsonSerializer.Deserialize<MealAnalysisResult>(insightsJson);
            if (analysis is null)
            {
                result.IsValid = false;
                result.Errors.Add("Failed to deserialize meal analysis JSON.");
                return result;
            }

            // Validate schema version
            if (string.IsNullOrWhiteSpace(analysis.SchemaVersion))
            {
                result.Warnings.Add("Schema version is missing.");
            }
            else if (analysis.SchemaVersion != expectedSchemaVersion)
            {
                result.Warnings.Add($"Schema version mismatch: expected {expectedSchemaVersion}, got {analysis.SchemaVersion}");
            }

            // Validate required fields
            if (analysis.FoodItems is null)
            {
                result.IsValid = false;
                result.Errors.Add("FoodItems array is null.");
            }
            else if (analysis.FoodItems.Count == 0)
            {
                result.Warnings.Add("No food items detected.");
            }
            else
            {
                // Validate food items
                for (int i = 0; i < analysis.FoodItems.Count; i++)
                {
                    var item = analysis.FoodItems[i];
                    if (string.IsNullOrWhiteSpace(item.Name))
                    {
                        result.Errors.Add($"FoodItem[{i}] has empty name.");
                        result.IsValid = false;
                    }
                    if (item.Confidence < 0.0 || item.Confidence > 1.0)
                    {
                        result.Errors.Add($"FoodItem[{i}] '{item.Name}' has invalid confidence: {item.Confidence}");
                        result.IsValid = false;
                    }
                }
            }

            // Validate confidence
            if (analysis.Confidence < 0.0 || analysis.Confidence > 1.0)
            {
                result.Errors.Add($"Overall confidence is out of range: {analysis.Confidence}");
                result.IsValid = false;
            }

            // Validate nutrition estimates if present
            if (analysis.Nutrition is not null)
            {
                if (analysis.Nutrition.TotalCalories.HasValue && analysis.Nutrition.TotalCalories < 0)
                {
                    result.Warnings.Add($"Negative total calories: {analysis.Nutrition.TotalCalories}");
                }
                if (analysis.Nutrition.Protein.HasValue && analysis.Nutrition.Protein < 0)
                {
                    result.Warnings.Add($"Negative protein: {analysis.Nutrition.Protein}");
                }
                if (analysis.Nutrition.Carbohydrates.HasValue && analysis.Nutrition.Carbohydrates < 0)
                {
                    result.Warnings.Add($"Negative carbohydrates: {analysis.Nutrition.Carbohydrates}");
                }
                if (analysis.Nutrition.Fat.HasValue && analysis.Nutrition.Fat < 0)
                {
                    result.Warnings.Add($"Negative fat: {analysis.Nutrition.Fat}");
                }
            }

            // Validate health insights if present
            if (analysis.HealthInsights is not null)
            {
                if (analysis.HealthInsights.HealthScore.HasValue)
                {
                    var score = analysis.HealthInsights.HealthScore.Value;
                    if (score < 0 || score > 10)
                    {
                        result.Warnings.Add($"Health score out of range (0-10): {score}");
                    }
                }
            }

            // Log validation result
            if (!result.IsValid)
            {
                _logger.LogWarning("Meal analysis validation failed with {ErrorCount} errors.", result.Errors.Count);
            }
            else if (result.Warnings.Any())
            {
                _logger.LogInformation("Meal analysis validation passed with {WarningCount} warnings.", result.Warnings.Count);
            }
            else
            {
                _logger.LogDebug("Meal analysis validation passed.");
            }
        }
        catch (JsonException ex)
        {
            result.IsValid = false;
            result.Errors.Add($"JSON parsing error: {ex.Message}");
            _logger.LogError(ex, "Failed to parse meal analysis JSON during validation.");
        }

        return result;
    }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}
