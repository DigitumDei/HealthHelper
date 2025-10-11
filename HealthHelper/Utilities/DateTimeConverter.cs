using System;

namespace HealthHelper.Utilities;

/// <summary>
/// Provides helpers for normalizing timestamps between UTC and the local timezone.
/// </summary>
public static class DateTimeConverter
{
    /// <summary>
    /// Normalizes <paramref name="value"/> to a UTC <see cref="DateTime"/>.
    /// </summary>
    private static DateTime NormalizeUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => value
        };
    }

    /// <summary>
    /// Converts the provided <see cref="DateTime"/> into the local timezone, assuming unspecified values
    /// with a non-zero time component originate from UTC storage (e.g. SQLite).
    /// </summary>
    public static DateTime ToLocal(DateTime value, TimeZoneInfo? timeZone = null)
    {
        var tz = timeZone ?? TimeZoneInfo.Local;

        return value.Kind switch
        {
            DateTimeKind.Local => value,
            DateTimeKind.Utc => TimeZoneInfo.ConvertTimeFromUtc(value, tz),
            DateTimeKind.Unspecified when value.TimeOfDay == TimeSpan.Zero => DateTime.SpecifyKind(value, DateTimeKind.Local),
            DateTimeKind.Unspecified => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(value, DateTimeKind.Utc), tz),
            _ => value
        };
    }

    /// <summary>
    /// Converts the provided UTC timestamp into its original local representation based on stored metadata.
    /// Falls back to <paramref name="fallbackTimeZone"/> or <see cref="TimeZoneInfo.Local"/> if metadata is incomplete.
    /// </summary>
    public static DateTime ToOriginalLocal(
        DateTime utcTimestamp,
        string? timeZoneId,
        int? offsetMinutes,
        TimeZoneInfo? fallbackTimeZone = null)
    {
        var utc = NormalizeUtc(utcTimestamp);

        var tz = ResolveTimeZone(timeZoneId);
        if (tz is not null)
        {
            return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, tz), DateTimeKind.Unspecified);
        }

        if (offsetMinutes is int offset)
        {
            return DateTime.SpecifyKind(utc.AddMinutes(offset), DateTimeKind.Unspecified);
        }

        var fallback = fallbackTimeZone ?? TimeZoneInfo.Local;
        return DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(utc, fallback), DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Resolves a <see cref="TimeZoneInfo"/> either by <paramref name="timeZoneId"/> or by creating a fixed-offset timezone.
    /// </summary>
    public static TimeZoneInfo? ResolveTimeZone(string? timeZoneId, int? offsetMinutes = null)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                // Fall back to offset below
            }
            catch (InvalidTimeZoneException)
            {
                // Fall back to offset below
            }
        }

        if (offsetMinutes is int offset)
        {
            var timeSpan = TimeSpan.FromMinutes(offset);
            var sign = offset >= 0 ? "+" : "-";
            var id = $"UTC{sign}{Math.Abs(timeSpan.Hours):00}:{Math.Abs(timeSpan.Minutes):00}";
            return TimeZoneInfo.CreateCustomTimeZone(id, timeSpan, id, id);
        }

        return null;
    }

    /// <summary>
    /// Returns the UTC inclusive-exclusive range for the local day represented by <paramref name="value"/>.
    /// </summary>
    public static (DateTime UtcStart, DateTime UtcEnd) GetUtcBoundsForLocalDay(DateTime value, TimeZoneInfo? timeZone = null)
    {
        var tz = timeZone ?? TimeZoneInfo.Local;
        var localDateTime = ToLocal(value, tz);
        var localMidnight = DateTime.SpecifyKind(localDateTime.Date, DateTimeKind.Unspecified);

        var utcStart = TimeZoneInfo.ConvertTimeToUtc(localMidnight, tz);
        var utcEnd = TimeZoneInfo.ConvertTimeToUtc(localMidnight.AddDays(1), tz);

        return (utcStart, utcEnd);
    }

    /// <summary>
    /// Returns the UTC offset in minutes for the provided timezone at the supplied UTC timestamp.
    /// </summary>
    public static int GetUtcOffsetMinutes(TimeZoneInfo timeZone, DateTime utcTimestamp)
    {
        var utc = NormalizeUtc(utcTimestamp);
        return (int)Math.Round(timeZone.GetUtcOffset(utc).TotalMinutes);
    }

    /// <summary>
    /// Captures the timezone identifier and offset metadata for a given UTC instant.
    /// </summary>
    public static (string? TimeZoneId, int OffsetMinutes) CaptureTimeZoneMetadata(DateTime utcTimestamp, TimeZoneInfo? timeZone = null)
    {
        var tz = timeZone ?? TimeZoneInfo.Local;
        var offsetMinutes = GetUtcOffsetMinutes(tz, utcTimestamp);
        return (tz.Id, offsetMinutes);
    }

    /// <summary>
    /// Formats an offset in minutes into ±HH:mm.
    /// </summary>
    public static string FormatOffset(int offsetMinutes)
    {
        var timeSpan = TimeSpan.FromMinutes(offsetMinutes);
        var sign = offsetMinutes >= 0 ? "+" : "-";
        return $"{sign}{Math.Abs(timeSpan.Hours):00}:{Math.Abs(timeSpan.Minutes):00}";
    }
}
