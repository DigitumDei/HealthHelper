using System.Text.Json.Serialization;

namespace HealthHelper.Models;

/// <summary>
/// Structured JSON schema for meal analysis results from the LLM.
/// This format ensures reliable parsing and enables versioning.
/// </summary>
public class MealAnalysisResult
{
    /// <summary>
    /// Schema version for evolution tracking and backward compatibility.
    /// Current version: "1.0"
    /// </summary>
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "1.0";

    /// <summary>
    /// List of detected food items in the meal.
    /// </summary>
    [JsonPropertyName("foodItems")]
    public List<FoodItem> FoodItems { get; set; } = new();

    /// <summary>
    /// Estimated nutritional information for the entire meal.
    /// </summary>
    [JsonPropertyName("nutrition")]
    public NutritionEstimate? Nutrition { get; set; }

    /// <summary>
    /// Overall health assessment and recommendations.
    /// </summary>
    [JsonPropertyName("healthInsights")]
    public HealthInsights? HealthInsights { get; set; }

    /// <summary>
    /// Confidence level of the analysis (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    /// <summary>
    /// Any warnings or errors encountered during analysis.
    /// </summary>
    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}

public class FoodItem
{
    /// <summary>
    /// Name of the food item.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Estimated portion size (e.g., "1 cup", "150g", "medium").
    /// </summary>
    [JsonPropertyName("portionSize")]
    public string? PortionSize { get; set; }

    /// <summary>
    /// Estimated calories for this item.
    /// </summary>
    [JsonPropertyName("calories")]
    public int? Calories { get; set; }

    /// <summary>
    /// Confidence in the detection of this food item (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}

public class NutritionEstimate
{
    /// <summary>
    /// Total estimated calories for the meal.
    /// </summary>
    [JsonPropertyName("totalCalories")]
    public int? TotalCalories { get; set; }

    /// <summary>
    /// Protein in grams.
    /// </summary>
    [JsonPropertyName("protein")]
    public double? Protein { get; set; }

    /// <summary>
    /// Carbohydrates in grams.
    /// </summary>
    [JsonPropertyName("carbohydrates")]
    public double? Carbohydrates { get; set; }

    /// <summary>
    /// Fat in grams.
    /// </summary>
    [JsonPropertyName("fat")]
    public double? Fat { get; set; }

    /// <summary>
    /// Fiber in grams.
    /// </summary>
    [JsonPropertyName("fiber")]
    public double? Fiber { get; set; }

    /// <summary>
    /// Sugar in grams.
    /// </summary>
    [JsonPropertyName("sugar")]
    public double? Sugar { get; set; }

    /// <summary>
    /// Sodium in milligrams.
    /// </summary>
    [JsonPropertyName("sodium")]
    public double? Sodium { get; set; }
}

public class HealthInsights
{
    /// <summary>
    /// Overall health score (0-10, where 10 is healthiest).
    /// </summary>
    [JsonPropertyName("healthScore")]
    public double? HealthScore { get; set; }

    /// <summary>
    /// Brief summary of the meal's health characteristics.
    /// </summary>
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    /// <summary>
    /// Positive aspects of the meal.
    /// </summary>
    [JsonPropertyName("positives")]
    public List<string> Positives { get; set; } = new();

    /// <summary>
    /// Areas for improvement.
    /// </summary>
    [JsonPropertyName("improvements")]
    public List<string> Improvements { get; set; } = new();

    /// <summary>
    /// Specific recommendations for healthier alternatives or additions.
    /// </summary>
    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = new();
}
