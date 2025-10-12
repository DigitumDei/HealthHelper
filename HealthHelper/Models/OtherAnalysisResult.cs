using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace HealthHelper.Models;

public class OtherAnalysisResult
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = new();
}
