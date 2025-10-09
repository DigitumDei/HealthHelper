using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HealthHelper.Models;

public class DailySummaryResult
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "1.0";

    [JsonPropertyName("totals")]
    public NutritionTotals Totals { get; set; } = new();

    [JsonPropertyName("balance")]
    public NutritionalBalance Balance { get; set; } = new();

    [JsonPropertyName("insights")]
    public List<string> Insights { get; set; } = new();

    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = new();

    [JsonPropertyName("mealsIncluded")]
    public List<MealReference> MealsIncluded { get; set; } = new();
}

public class NutritionTotals
{
    [JsonPropertyName("calories")]
    public double? Calories { get; set; }

    [JsonPropertyName("protein")]
    public double? Protein { get; set; }

    [JsonPropertyName("carbohydrates")]
    public double? Carbohydrates { get; set; }

    [JsonPropertyName("fat")]
    public double? Fat { get; set; }

    [JsonPropertyName("fiber")]
    public double? Fiber { get; set; }

    [JsonPropertyName("sugar")]
    public double? Sugar { get; set; }

    [JsonPropertyName("sodium")]
    public double? Sodium { get; set; }
}

public class NutritionalBalance
{
    [JsonPropertyName("overall")]
    public string? Overall { get; set; }

    [JsonPropertyName("macroBalance")]
    public string? MacroBalance { get; set; }

    [JsonPropertyName("timing")]
    public string? Timing { get; set; }

    [JsonPropertyName("variety")]
    public string? Variety { get; set; }
}

public class MealReference
{
    [JsonPropertyName("entryId")]
    public int EntryId { get; set; }

    [JsonPropertyName("capturedAt")]
    public DateTime CapturedAt { get; set; }

    [JsonPropertyName("summary")]
    public string? Summary { get; set; }
}
