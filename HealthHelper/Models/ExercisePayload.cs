namespace HealthHelper.Models;

public class ExercisePayload : IEntryPayload
{
    public string? Description { get; set; }
    public string? ExerciseType { get; set; }
    public string? PreviewBlobPath { get; set; }
    public string? ScreenshotBlobPath { get; set; }
}
