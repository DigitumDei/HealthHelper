using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HealthHelper.Models;

public class SleepAnalysisResult
{
    [JsonPropertyName("durationHours")]
    public double? DurationHours { get; set; }

    [JsonPropertyName("sleepScore")]
    public double? SleepScore { get; set; }

    [JsonPropertyName("qualitySummary")]
    public string? QualitySummary { get; set; }

    [JsonPropertyName("environmentNotes")]
    public List<string> EnvironmentNotes { get; set; } = new();

    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = new();
}
