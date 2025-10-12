using System;
using System.Text.Json.Serialization;

namespace HealthHelper.Models;

public class DailySummaryPayload : IEntryPayload
{
    public int SchemaVersion { get; set; } = 1;
    public int MealCount { get; set; }

    [JsonIgnore]
    public int EntryCount
    {
        get => MealCount;
        set => MealCount = value;
    }
    public DateTime GeneratedAt { get; set; }
    public string? GeneratedAtTimeZoneId { get; set; }
    public int? GeneratedAtOffsetMinutes { get; set; }
}
