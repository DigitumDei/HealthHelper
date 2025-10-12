#if ANDROID
using System.Globalization;
using AndroidX.ExifInterface.Media;
using Android.Util;

namespace HealthHelper.Utilities;

public static partial class ImageMetadataExtractor
{
    private static partial void FillPlatformMetadata(string absolutePath, PlatformMetadata metadata)
    {
        try
        {
            using var exif = new ExifInterface(absolutePath);

            var dateTimeString = exif.GetAttribute(ExifInterface.TagDatetimeOriginal)
                ?? exif.GetAttribute(ExifInterface.TagDatetime);

            if (string.IsNullOrWhiteSpace(dateTimeString))
            {
                return;
            }

            if (!DateTime.TryParseExact(
                    dateTimeString,
                    "yyyy:MM:dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedLocal))
            {
                return;
            }

            var offsetString = exif.GetAttribute(ExifInterface.TagOffsetTimeOriginal)
                ?? exif.GetAttribute(ExifInterface.TagOffsetTime);

            TimeSpan? offset = null;
            if (!string.IsNullOrWhiteSpace(offsetString))
            {
                if (TimeSpan.TryParse(offsetString, CultureInfo.InvariantCulture, out var parsedOffset))
                {
                    offset = parsedOffset;
                }
                else
                {
                    // Offsets may appear as "+0530" without colon
                    if (offsetString.Length == 5 &&
                        int.TryParse(offsetString.AsSpan(1, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) &&
                        int.TryParse(offsetString.AsSpan(3, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
                    {
                        var sign = offsetString.StartsWith("-", StringComparison.Ordinal) ? -1 : 1;
                        offset = new TimeSpan(sign * hours, sign * minutes, 0);
                    }
                }
            }

            var localDateTime = DateTime.SpecifyKind(parsedLocal, DateTimeKind.Unspecified);
            DateTime capturedUtc;
            if (offset is TimeSpan utcOffset)
            {
                capturedUtc = localDateTime - utcOffset;
                metadata.CapturedAtOffsetMinutes = (int)utcOffset.TotalMinutes;
            }
            else
            {
                capturedUtc = DateTime.SpecifyKind(localDateTime, DateTimeKind.Local).ToUniversalTime();
                metadata.CapturedAtOffsetMinutes = DateTimeConverter.GetUtcOffsetMinutes(TimeZoneInfo.Local, capturedUtc);
                metadata.CapturedAtTimeZoneId = TimeZoneInfo.Local.Id;
            }

            metadata.CapturedAtUtc = DateTime.SpecifyKind(capturedUtc, DateTimeKind.Utc);
            metadata.HasExifTimestamp = true;
        }
        catch (Exception ex)
        {
            Log.Warn("HealthHelper.Metadata", $"Failed to read EXIF metadata from {absolutePath}: {ex}");
        }
    }
}
#endif
