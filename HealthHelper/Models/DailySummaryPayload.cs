using System;

namespace HealthHelper.Models;

public class DailySummaryPayload : IEntryPayload
{
    public int SchemaVersion { get; set; } = 1;
    public int MealCount { get; set; }
    public DateTime GeneratedAt { get; set; }
}
