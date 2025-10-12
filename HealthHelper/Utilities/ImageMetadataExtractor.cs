using Microsoft.Maui.Storage;

namespace HealthHelper.Utilities;

public static partial class ImageMetadataExtractor
{
    public static SharedImageMetadata Extract(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            throw new ArgumentException("File path must be provided.", nameof(absolutePath));
        }

        if (!File.Exists(absolutePath))
        {
            throw new FileNotFoundException("Image file not found.", absolutePath);
        }

        var platformMetadata = ExtractPlatformMetadata(absolutePath);

        var fileInfo = new FileInfo(absolutePath);
        var createdUtc = fileInfo.CreationTimeUtc;
        if (createdUtc == DateTime.MinValue)
        {
            createdUtc = fileInfo.LastWriteTimeUtc;
        }
        if (createdUtc == DateTime.MinValue)
        {
            createdUtc = DateTime.UtcNow;
        }

        var capturedUtc = platformMetadata.CapturedAtUtc ?? createdUtc;
        capturedUtc = DateTime.SpecifyKind(capturedUtc, DateTimeKind.Utc);

        var capturedAtTimeZoneId = platformMetadata.CapturedAtTimeZoneId;
        var capturedAtOffsetMinutes = platformMetadata.CapturedAtOffsetMinutes;

        if (capturedAtOffsetMinutes is null)
        {
            var timeZone = DateTimeConverter.ResolveTimeZone(
                capturedAtTimeZoneId,
                platformMetadata.CapturedAtOffsetMinutes);
            if (timeZone is not null)
            {
                capturedAtOffsetMinutes = DateTimeConverter.GetUtcOffsetMinutes(timeZone, capturedUtc);
                capturedAtTimeZoneId = timeZone.Id;
            }
            else
            {
                var (tzId, offset) = DateTimeConverter.CaptureTimeZoneMetadata(capturedUtc);
                capturedAtTimeZoneId = tzId;
                capturedAtOffsetMinutes = offset;
            }
        }

        return new SharedImageMetadata
        {
            CapturedAtUtc = capturedUtc,
            CapturedAtTimeZoneId = capturedAtTimeZoneId,
            CapturedAtOffsetMinutes = capturedAtOffsetMinutes,
            HasExifTimestamp = platformMetadata.HasExifTimestamp,
            IsLikelyScreenshot = !platformMetadata.HasExifTimestamp
        };
    }

    private static PlatformMetadata ExtractPlatformMetadata(string absolutePath)
    {
        return GetPlatformMetadata(absolutePath);
    }

    private static PlatformMetadata GetPlatformMetadata(string absolutePath)
    {
        var metadata = new PlatformMetadata();
        FillPlatformMetadata(absolutePath, metadata);
        return metadata;
    }

    private static partial void FillPlatformMetadata(string absolutePath, PlatformMetadata metadata);

    protected sealed class PlatformMetadata
    {
        public DateTime? CapturedAtUtc { get; set; }
        public string? CapturedAtTimeZoneId { get; set; }
        public int? CapturedAtOffsetMinutes { get; set; }
        public bool HasExifTimestamp { get; set; }
    }
}
