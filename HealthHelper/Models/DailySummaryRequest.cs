using System;
using System.Collections.Generic;

namespace HealthHelper.Models;

public class DailySummaryRequest
{
    public int SummaryEntryId { get; set; }
    public DateTime SummaryDate { get; set; }
    public string? SummaryTimeZoneId { get; set; }
    public int? SummaryUtcOffsetMinutes { get; set; }
    public List<DailySummaryMealContext> Meals { get; set; } = new();
}

public class DailySummaryMealContext
{
    public int EntryId { get; set; }
    public DateTime CapturedAt { get; set; }
    public DateTime CapturedAtLocal { get; set; }
    public string? TimeZoneId { get; set; }
    public int? UtcOffsetMinutes { get; set; }
    public string? Description { get; set; }
    public MealAnalysisResult? Analysis { get; set; }
}
