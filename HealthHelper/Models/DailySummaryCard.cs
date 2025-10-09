using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HealthHelper.Models;

public partial class DailySummaryCard : ObservableObject
{
    public int EntryId { get; init; }
    public int MealCount { get; private set; }
    public DateTime GeneratedAt { get; private set; }

    public DateTime LocalGeneratedAt
    {
        get
        {
            if (GeneratedAt == default)
            {
                return GeneratedAt;
            }

            return GeneratedAt.Kind == DateTimeKind.Utc
                ? GeneratedAt.ToLocalTime()
                : GeneratedAt;
        }
    }

    [ObservableProperty]
    private ProcessingStatus processingStatus;

    [ObservableProperty]
    private bool isOutdated;

    public bool IsClickable => ProcessingStatus == ProcessingStatus.Completed;

    public DailySummaryCard(int entryId, int mealCount, DateTime generatedAt, ProcessingStatus status)
    {
        EntryId = entryId;
        MealCount = mealCount;
        GeneratedAt = generatedAt;
        processingStatus = status;
    }

    public void RefreshMetadata(int mealCount, DateTime generatedAt)
    {
        MealCount = mealCount;
        GeneratedAt = generatedAt;
        OnPropertyChanged(nameof(MealCount));
        OnPropertyChanged(nameof(GeneratedAt));
        OnPropertyChanged(nameof(LocalGeneratedAt));
    }

    partial void OnProcessingStatusChanged(ProcessingStatus value)
    {
        OnPropertyChanged(nameof(IsClickable));
    }
}
