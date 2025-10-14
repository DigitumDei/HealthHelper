using System;
using System.Collections.Generic;

namespace HealthHelper.Models;

/// <summary>
/// Represents a summary of tracked activity for a single day.
/// </summary>
public class DaySummary
{
    public DateTime Date { get; init; }
    public int MealCount { get; init; }
    public int ExerciseCount { get; init; }
    public int SleepCount { get; init; }
    public int OtherCount { get; init; }
    public int PendingCount { get; init; }
    public int CompletedCount { get; init; }
    public bool HasPendingOrFailedAnalysis { get; init; }
    public ProcessingStatus? DailySummaryStatus { get; init; }
    public int? DailySummaryEntryId { get; init; }
    public IReadOnlyList<DayPreview> Previews { get; init; } = Array.Empty<DayPreview>();

    public int TotalCount => MealCount + ExerciseCount + SleepCount + OtherCount + PendingCount;

    public bool HasDailySummary => DailySummaryEntryId.HasValue;
}

/// <summary>
/// Represents a preview asset for a tracked entry displayed within a day summary.
/// </summary>
public class DayPreview
{
    public DayPreview(int entryId, EntryType entryType, string relativePath)
    {
        EntryId = entryId;
        EntryType = entryType;
        RelativePath = relativePath;
    }

    public int EntryId { get; }
    public EntryType EntryType { get; }
    public string RelativePath { get; }
}
