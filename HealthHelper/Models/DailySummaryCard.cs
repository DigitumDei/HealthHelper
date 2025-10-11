using System;
using CommunityToolkit.Mvvm.ComponentModel;
using HealthHelper.Utilities;

namespace HealthHelper.Models;

public partial class DailySummaryCard : ObservableObject
{
    public int EntryId { get; init; }
    public int MealCount { get; private set; }
    public DateTime GeneratedAt { get; private set; }
    public string? GeneratedAtTimeZoneId { get; private set; }
    public int? GeneratedAtOffsetMinutes { get; private set; }

    public DateTime LocalGeneratedAt
    {
        get
        {
            if (GeneratedAt == default)
            {
                return GeneratedAt;
            }

            return DateTimeConverter.ToOriginalLocal(GeneratedAt, GeneratedAtTimeZoneId, GeneratedAtOffsetMinutes);
        }
    }

    [ObservableProperty]
    private ProcessingStatus processingStatus;

    [ObservableProperty]
    private bool isOutdated;

    public bool IsClickable => ProcessingStatus == ProcessingStatus.Completed;

    public DailySummaryCard(
        int entryId,
        int mealCount,
        DateTime generatedAt,
        string? generatedAtTimeZoneId,
        int? generatedAtOffsetMinutes,
        ProcessingStatus status)
    {
        EntryId = entryId;
        MealCount = mealCount;
        GeneratedAt = generatedAt;
        GeneratedAtTimeZoneId = generatedAtTimeZoneId;
        GeneratedAtOffsetMinutes = generatedAtOffsetMinutes;
        processingStatus = status;
    }

    public void RefreshMetadata(int mealCount, DateTime generatedAt, string? generatedAtTimeZoneId, int? generatedAtOffsetMinutes)
    {
        MealCount = mealCount;
        GeneratedAt = generatedAt;
        GeneratedAtTimeZoneId = generatedAtTimeZoneId;
        GeneratedAtOffsetMinutes = generatedAtOffsetMinutes;
        OnPropertyChanged(nameof(MealCount));
        OnPropertyChanged(nameof(GeneratedAt));
        OnPropertyChanged(nameof(LocalGeneratedAt));
    }

    partial void OnProcessingStatusChanged(ProcessingStatus value)
    {
        OnPropertyChanged(nameof(IsClickable));
    }
}
