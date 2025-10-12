namespace HealthHelper.Models;

public partial class SleepEntry : TrackedEntryCard
{
    public SleepEntry(
        int entryId,
        string previewPath,
        string? description,
        DateTime capturedAtUtc,
        string? capturedAtTimeZoneId,
        int? capturedAtOffsetMinutes,
        ProcessingStatus processingStatus)
        : base(
            entryId,
            EntryType.Sleep,
            capturedAtUtc,
            capturedAtTimeZoneId,
            capturedAtOffsetMinutes,
            processingStatus)
    {
        PreviewPath = previewPath;
        Description = description;
    }

    public string PreviewPath { get; }
    public string? Description { get; }
}
