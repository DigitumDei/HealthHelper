using System;

namespace HealthHelper.Models;

public enum EntryType
{
    Unknown,
    Meal,
    Exercise,
    Sleep,
    Other,
    DailySummary
}

public static class EntryTypeHelper
{
    public static EntryType FromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EntryType.Unknown;
        }

        return value.Trim() switch
        {
            { } s when s.Equals("Meal", StringComparison.OrdinalIgnoreCase) => EntryType.Meal,
            { } s when s.Equals("Exercise", StringComparison.OrdinalIgnoreCase) => EntryType.Exercise,
            { } s when s.Equals("Sleep", StringComparison.OrdinalIgnoreCase) => EntryType.Sleep,
            { } s when s.Equals("Other", StringComparison.OrdinalIgnoreCase) => EntryType.Other,
            { } s when s.Equals("DailySummary", StringComparison.OrdinalIgnoreCase) => EntryType.DailySummary,
            _ => EntryType.Unknown
        };
    }

    public static bool TryParse(string? value, out EntryType entryType)
    {
        entryType = FromString(value);
        if (entryType == EntryType.Unknown && !IsUnknownToken(value))
        {
            return false;
        }

        return true;
    }

    public static string ToStorageString(this EntryType entryType)
    {
        return entryType switch
        {
            EntryType.Unknown => "Unknown",
            EntryType.Meal => "Meal",
            EntryType.Exercise => "Exercise",
            EntryType.Sleep => "Sleep",
            EntryType.Other => "Other",
            EntryType.DailySummary => "DailySummary",
            _ => "Unknown"
        };
    }

    private static bool IsUnknownToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value) || value.Equals("Unknown", StringComparison.OrdinalIgnoreCase);
    }
}
