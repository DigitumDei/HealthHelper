using CommunityToolkit.Mvvm.ComponentModel;

namespace HealthHelper.Models;

public partial class MealPhoto : ObservableObject
{
    public int EntryId { get; init; }
    public string FullPath { get; init; }
    public string OriginalPath { get; init; }
    public string Description { get; init; }
    public DateTime CapturedAt { get; init; }

    [ObservableProperty]
    private ProcessingStatus processingStatus;

    public bool IsClickable => ProcessingStatus == ProcessingStatus.Completed;

    public MealPhoto(int entryId, string fullPath, string originalPath, string description, DateTime capturedAt, ProcessingStatus processingStatus)
    {
        EntryId = entryId;
        FullPath = fullPath;
        OriginalPath = originalPath;
        Description = description;
        CapturedAt = capturedAt;
        ProcessingStatus = processingStatus;
    }

    partial void OnProcessingStatusChanged(ProcessingStatus value)
    {
        OnPropertyChanged(nameof(IsClickable));
    }
}
