namespace HealthHelper.Models;

public partial class ExerciseEntry : TrackedEntryCard
{
    public ExerciseEntry(
        int entryId,
        string previewPath,
        string? screenshotPath,
        string? description,
        string? exerciseType,
        DateTime capturedAtUtc,
        string? capturedAtTimeZoneId,
        int? capturedAtOffsetMinutes,
        ProcessingStatus processingStatus)
        : base(
            entryId,
            EntryType.Exercise,
            capturedAtUtc,
            capturedAtTimeZoneId,
            capturedAtOffsetMinutes,
            processingStatus)
    {
        PreviewPath = previewPath;
        ScreenshotPath = screenshotPath;
        Description = description;
        ExerciseType = exerciseType;
    }

    public string PreviewPath { get; }
    public string? ScreenshotPath { get; }
    public string? Description { get; }
    public string? ExerciseType { get; }
}
