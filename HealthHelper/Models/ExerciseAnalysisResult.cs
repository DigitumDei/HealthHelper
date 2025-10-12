using System.Text.Json.Serialization;

namespace HealthHelper.Models;

public class ExerciseAnalysisResult
{
    [JsonPropertyName("schemaVersion")]
    public string SchemaVersion { get; set; } = "1.0";

    [JsonPropertyName("activityType")]
    public string? ActivityType { get; set; }

    [JsonPropertyName("metrics")]
    public ExerciseMetrics Metrics { get; set; } = new();

    [JsonPropertyName("insights")]
    public ExerciseInsights? Insights { get; set; }

    [JsonPropertyName("warnings")]
    public List<string> Warnings { get; set; } = new();
}

public class ExerciseMetrics
{
    [JsonPropertyName("distance")]
    public double? Distance { get; set; }

    [JsonPropertyName("distanceUnit")]
    public string? DistanceUnit { get; set; }

    [JsonPropertyName("durationMinutes")]
    public double? DurationMinutes { get; set; }

    [JsonPropertyName("averagePace")]
    public string? AveragePace { get; set; }

    [JsonPropertyName("averageSpeed")]
    public double? AverageSpeed { get; set; }

    [JsonPropertyName("speedUnit")]
    public string? SpeedUnit { get; set; }

    [JsonPropertyName("calories")]
    public double? Calories { get; set; }

    [JsonPropertyName("averageHeartRate")]
    public double? AverageHeartRate { get; set; }

    [JsonPropertyName("maxHeartRate")]
    public double? MaxHeartRate { get; set; }

    [JsonPropertyName("steps")]
    public int? Steps { get; set; }

    [JsonPropertyName("elevationGain")]
    public double? ElevationGain { get; set; }

    [JsonPropertyName("elevationUnit")]
    public string? ElevationUnit { get; set; }
}

public class ExerciseInsights
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("positives")]
    public List<string> Positives { get; set; } = new();

    [JsonPropertyName("improvements")]
    public List<string> Improvements { get; set; } = new();

    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = new();
}
