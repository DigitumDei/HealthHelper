namespace HealthHelper.Services.Navigation;

public enum HistoricalViewLevel
{
    Today = 0,
    Week = 1,
    Month = 2,
    Year = 3,
    // Day sits outside the high-level scale and represents a drill-down detail view.
    Day = 4
}
