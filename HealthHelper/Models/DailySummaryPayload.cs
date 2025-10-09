using System;

namespace HealthHelper.Models;

public class DailySummaryPayload : IEntryPayload
{
    public int MealCount { get; set; }
    public DateTime GeneratedAt { get; set; }
}
