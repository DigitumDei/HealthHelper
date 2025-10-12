using System;
using System.Collections.Generic;
using System.Globalization;

namespace HealthHelper.Services.Navigation;

public sealed class NavigationBreadcrumb
{
    private static readonly Dictionary<HistoricalViewLevel, string> RouteMap = new()
    {
        { HistoricalViewLevel.Today, "today" },
        { HistoricalViewLevel.Week, "week" },
        { HistoricalViewLevel.Month, "month" },
        { HistoricalViewLevel.Year, "year" },
        { HistoricalViewLevel.Day, "day" }
    };

    public NavigationBreadcrumb(HistoricalViewLevel level, DateTime date, string? label = null)
    {
        Level = level;
        Date = date;
        Label = label ?? BuildDefaultLabel(level, date);
    }

    public HistoricalViewLevel Level { get; }

    public DateTime Date { get; }

    public string Label { get; }

    public string Route => RouteMap[Level];

    public IDictionary<string, object> BuildNavigationParameters()
    {
        var parameters = new Dictionary<string, object>();
        switch (Level)
        {
            case HistoricalViewLevel.Day:
                parameters["Date"] = Date;
                break;
            case HistoricalViewLevel.Week:
                parameters["WeekStart"] = Date;
                break;
            case HistoricalViewLevel.Month:
                parameters["Year"] = Date.Year;
                parameters["Month"] = Date.Month;
                break;
            case HistoricalViewLevel.Year:
                parameters["Year"] = Date.Year;
                break;
        }

        return parameters;
    }

    private static string BuildDefaultLabel(HistoricalViewLevel level, DateTime date)
    {
        // TODO: Localise breadcrumb labels once shared resources are available.
        return level switch
        {
            HistoricalViewLevel.Today => "Today",
            HistoricalViewLevel.Day => date.ToString("D", CultureInfo.CurrentCulture),
            HistoricalViewLevel.Week => $"Week of {date:MMM d}",
            HistoricalViewLevel.Month => date.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            HistoricalViewLevel.Year => date.ToString("yyyy", CultureInfo.InvariantCulture),
            _ => date.ToString(CultureInfo.CurrentCulture)
        };
    }
}
