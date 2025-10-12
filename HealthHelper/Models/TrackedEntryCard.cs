using CommunityToolkit.Mvvm.ComponentModel;
using HealthHelper.Utilities;

namespace HealthHelper.Models;

public abstract partial class TrackedEntryCard : ObservableObject
{
    protected TrackedEntryCard(
        int entryId,
        string entryType,
        DateTime capturedAtUtc,
        string? capturedAtTimeZoneId,
        int? capturedAtOffsetMinutes,
        ProcessingStatus processingStatus)
    {
        EntryId = entryId;
        EntryType = entryType;
        CapturedAtUtc = capturedAtUtc;
        CapturedAtTimeZoneId = capturedAtTimeZoneId;
        CapturedAtOffsetMinutes = capturedAtOffsetMinutes;
        ProcessingStatus = processingStatus;
    }

    public int EntryId { get; }
    public string EntryType { get; }
    public DateTime CapturedAtUtc { get; }
    public string? CapturedAtTimeZoneId { get; }
    public int? CapturedAtOffsetMinutes { get; }

    public DateTime LocalCapturedAt => DateTimeConverter.ToOriginalLocal(
        CapturedAtUtc,
        CapturedAtTimeZoneId,
        CapturedAtOffsetMinutes);

    [ObservableProperty]
    private ProcessingStatus processingStatus;

    public bool IsClickable => ProcessingStatus == ProcessingStatus.Completed;

    partial void OnProcessingStatusChanged(ProcessingStatus value)
    {
        OnPropertyChanged(nameof(IsClickable));
    }
}
