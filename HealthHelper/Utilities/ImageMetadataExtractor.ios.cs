#if IOS || MACCATALYST
using System.Globalization;
using Foundation;
using ImageIO;

namespace HealthHelper.Utilities;

public static partial class ImageMetadataExtractor
{
    private static partial void FillPlatformMetadata(string absolutePath, PlatformMetadata metadata)
    {
        try
        {
            using var url = NSUrl.CreateFileUrl(absolutePath);
            using var source = CGImageSource.FromUrl(url);
            if (source is null)
            {
                return;
            }

            using var properties = source.CopyProperties((NSDictionary?)null, 0);
            if (properties is null)
            {
                return;
            }

            if (!properties.ContainsKey(CGImageProperties.ExifDictionary))
            {
                return;
            }

            if (properties[CGImageProperties.ExifDictionary] is not NSDictionary exifDict)
            {
                return;
            }

            if (!TryGetString(exifDict, CGImageProperties.ExifDateTimeOriginal, out var dateString) &&
                !TryGetString(exifDict, CGImageProperties.ExifDateTimeDigitized, out dateString))
            {
                return;
            }

            if (!DateTime.TryParseExact(
                    dateString,
                    "yyyy:MM:dd HH:mm:ss",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedLocal))
            {
                return;
            }

            TimeSpan? offset = null;
            if (TryGetString(exifDict, CGImageProperties.ExifOffsetTimeOriginal, out var offsetString) ||
                TryGetString(exifDict, CGImageProperties.ExifOffsetTime, out offsetString))
            {
                if (TimeSpan.TryParse(offsetString, CultureInfo.InvariantCulture, out var parsed))
                {
                    offset = parsed;
                }
                else if (offsetString.Length == 5 &&
                         int.TryParse(offsetString.AsSpan(1, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var hours) &&
                         int.TryParse(offsetString.AsSpan(3, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
                {
                    var sign = offsetString.StartsWith("-", StringComparison.Ordinal) ? -1 : 1;
                    offset = new TimeSpan(sign * hours, sign * minutes, 0);
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
        catch
        {
            // Ignore metadata failures; fall back to file metadata.
        }
    }

    private static bool TryGetString(NSDictionary dictionary, string key, out string value)
    {
        value = string.Empty;
        if (!dictionary.ContainsKey((NSString)key))
        {
            return false;
        }

        if (dictionary[(NSString)key] is NSString nsString)
        {
            value = nsString.ToString();
            return !string.IsNullOrWhiteSpace(value);
        }

        return false;
    }
}
#endif
