namespace HealthHelper.Utilities;

public sealed class SharedImageMetadata
{
    public DateTime CapturedAtUtc { get; init; }
    public string? CapturedAtTimeZoneId { get; init; }
    public int? CapturedAtOffsetMinutes { get; init; }
    public bool HasExifTimestamp { get; init; }
    public bool IsLikelyScreenshot { get; init; }
}
