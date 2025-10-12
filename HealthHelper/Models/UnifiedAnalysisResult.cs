using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HealthHelper.Models;

public class UnifiedAnalysisResult
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "1.0";

    [JsonPropertyName("entryType")]
    public string EntryType { get; set; } = "Other";

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }

    [JsonPropertyName("mealAnalysis")]
    public MealAnalysisResult? MealAnalysis { get; set; }

    [JsonPropertyName("exerciseAnalysis")]
    public ExerciseAnalysisResult? ExerciseAnalysis { get; set; }

    [JsonPropertyName("sleepAnalysis")]
    public SleepAnalysisResult? SleepAnalysis { get; set; }

    [JsonPropertyName("otherAnalysis")]
    public OtherAnalysisResult? OtherAnalysis { get; set; }

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}
