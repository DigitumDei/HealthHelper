using HealthHelper.Utilities;
using Xunit;

namespace HealthHelper.Tests.Utilities;

public class DateTimeConverterTests
{
    [Fact]
    public void GetUtcBoundsForLocalDay_UsesLocalMidnight_ForPositiveOffset()
    {
        var tz = TimeZoneInfo.CreateCustomTimeZone("UTC+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");
        var localDate = new DateTime(2025, 1, 10);

        var (utcStart, utcEnd) = DateTimeConverter.GetUtcBoundsForLocalDay(localDate, tz);

        Assert.Equal(DateTime.SpecifyKind(new DateTime(2025, 1, 9, 22, 0, 0), DateTimeKind.Utc), utcStart);
        Assert.Equal(DateTime.SpecifyKind(new DateTime(2025, 1, 10, 22, 0, 0), DateTimeKind.Utc), utcEnd);
    }

    [Fact]
    public void GetUtcBoundsForLocalDay_NormalizesUtcReference_ForNegativeOffset()
    {
        var tz = TimeZoneInfo.CreateCustomTimeZone("UTC-8", TimeSpan.FromHours(-8), "UTC-8", "UTC-8");
        var utcReference = DateTime.SpecifyKind(new DateTime(2025, 1, 10, 6, 30, 0), DateTimeKind.Utc);

        var (utcStart, utcEnd) = DateTimeConverter.GetUtcBoundsForLocalDay(utcReference, tz);

        Assert.Equal(DateTime.SpecifyKind(new DateTime(2025, 1, 9, 8, 0, 0), DateTimeKind.Utc), utcStart);
        Assert.Equal(DateTime.SpecifyKind(new DateTime(2025, 1, 10, 8, 0, 0), DateTimeKind.Utc), utcEnd);
    }

    [Fact]
    public void ToLocal_TreatsUnspecifiedTimestampAsUtcStorage()
    {
        var tz = TimeZoneInfo.CreateCustomTimeZone("UTC+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");
        var storedUtc = new DateTime(2025, 1, 10, 0, 30, 0); // Unspecified kind coming from SQLite

        var local = DateTimeConverter.ToLocal(storedUtc, tz);

        Assert.Equal(new DateTime(2025, 1, 10, 2, 30, 0), local);
    }

    [Fact]
    public void ToLocal_KeepsUnspecifiedMidnightAsLocal()
    {
        var tz = TimeZoneInfo.CreateCustomTimeZone("UTC-5", TimeSpan.FromHours(-5), "UTC-5", "UTC-5");
        var localMidnight = new DateTime(2025, 1, 10); // Unspecified with zero time component

        var local = DateTimeConverter.ToLocal(localMidnight, tz);

        Assert.Equal(DateTimeKind.Local, local.Kind);
        Assert.Equal(localMidnight, local);
    }

    [Fact]
    public void ToOriginalLocal_UsesTimeZoneIdMetadata()
    {
        var localZone = TimeZoneInfo.Local;
        var utcTimestamp = DateTime.SpecifyKind(new DateTime(2025, 6, 15, 12, 0, 0), DateTimeKind.Utc);

        var expected = TimeZoneInfo.ConvertTimeFromUtc(utcTimestamp, localZone);
        var converted = DateTimeConverter.ToOriginalLocal(utcTimestamp, localZone.Id, null, localZone);

        Assert.Equal(expected, converted);
    }

    [Fact]
    public void ToOriginalLocal_FallsBackToOffsetWhenTimeZoneUnknown()
    {
        var utcTimestamp = DateTime.SpecifyKind(new DateTime(2025, 1, 10, 1, 0, 0), DateTimeKind.Utc);

        var converted = DateTimeConverter.ToOriginalLocal(utcTimestamp, null, 120);

        Assert.Equal(DateTime.SpecifyKind(new DateTime(2025, 1, 10, 3, 0, 0), DateTimeKind.Unspecified), converted);
    }

    [Fact]
    public void CaptureTimeZoneMetadata_ReportsCurrentOffset()
    {
        var tz = TimeZoneInfo.CreateCustomTimeZone("UTC+3", TimeSpan.FromHours(3), "UTC+3", "UTC+3");
        var utcTimestamp = DateTime.SpecifyKind(new DateTime(2025, 3, 15, 12, 0, 0), DateTimeKind.Utc);

        var (timeZoneId, offsetMinutes) = DateTimeConverter.CaptureTimeZoneMetadata(utcTimestamp, tz);

        Assert.Equal(tz.Id, timeZoneId);
        Assert.Equal(180, offsetMinutes);
    }

    [Theory]
    [InlineData(0, "+00:00")]
    [InlineData(90, "+01:30")]
    [InlineData(-300, "-05:00")]
    public void FormatOffset_ProducesExpectedNotation(int offsetMinutes, string expected)
    {
        var formatted = DateTimeConverter.FormatOffset(offsetMinutes);

        Assert.Equal(expected, formatted);
    }
}
