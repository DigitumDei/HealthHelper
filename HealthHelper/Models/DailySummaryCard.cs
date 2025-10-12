using System;
using CommunityToolkit.Mvvm.ComponentModel;
using HealthHelper.Utilities;

namespace HealthHelper.Models;

public partial class DailySummaryCard : ObservableObject
{
    public int EntryId { get; init; }
    public int EntryCount { get; private set; }
    public int MealCount => EntryCount;
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
        int entryCount,
        DateTime generatedAt,
        string? generatedAtTimeZoneId,
        int? generatedAtOffsetMinutes,
        ProcessingStatus status)
    {
        EntryId = entryId;
        EntryCount = entryCount;
        GeneratedAt = generatedAt;
        GeneratedAtTimeZoneId = generatedAtTimeZoneId;
        GeneratedAtOffsetMinutes = generatedAtOffsetMinutes;
        processingStatus = status;
    }

    public void RefreshMetadata(int entryCount, DateTime generatedAt, string? generatedAtTimeZoneId, int? generatedAtOffsetMinutes)
    {
        EntryCount = entryCount;
        GeneratedAt = generatedAt;
        GeneratedAtTimeZoneId = generatedAtTimeZoneId;
        GeneratedAtOffsetMinutes = generatedAtOffsetMinutes;
        OnPropertyChanged(nameof(EntryCount));
        OnPropertyChanged(nameof(MealCount));
        OnPropertyChanged(nameof(GeneratedAt));
        OnPropertyChanged(nameof(LocalGeneratedAt));
    }

    partial void OnProcessingStatusChanged(ProcessingStatus value)
    {
        OnPropertyChanged(nameof(IsClickable));
    }
}
