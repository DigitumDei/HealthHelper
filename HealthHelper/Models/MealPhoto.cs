using CommunityToolkit.Mvvm.ComponentModel;
using HealthHelper.Utilities;

namespace HealthHelper.Models;

public partial class MealPhoto : ObservableObject
{
    public int EntryId { get; init; }
    public string FullPath { get; init; }
    public string OriginalPath { get; init; }
    public string Description { get; init; }
    public DateTime CapturedAt { get; init; }
    public string? CapturedAtTimeZoneId { get; init; }
    public int? CapturedAtOffsetMinutes { get; init; }

    public DateTime LocalCapturedAt
    {
        get
        {
            return DateTimeConverter.ToOriginalLocal(CapturedAt, CapturedAtTimeZoneId, CapturedAtOffsetMinutes);
        }
    }

    [ObservableProperty]
    private ProcessingStatus processingStatus;

    public bool IsClickable => ProcessingStatus == ProcessingStatus.Completed;

    public MealPhoto(
        int entryId,
        string fullPath,
        string originalPath,
        string description,
        DateTime capturedAt,
        string? capturedAtTimeZoneId,
        int? capturedAtOffsetMinutes,
        ProcessingStatus processingStatus)
    {
        EntryId = entryId;
        FullPath = fullPath;
        OriginalPath = originalPath;
        Description = description;
        CapturedAt = capturedAt;
        CapturedAtTimeZoneId = capturedAtTimeZoneId;
        CapturedAtOffsetMinutes = capturedAtOffsetMinutes;
        ProcessingStatus = processingStatus;
    }

    partial void OnProcessingStatusChanged(ProcessingStatus value)
    {
        OnPropertyChanged(nameof(IsClickable));
    }
}
