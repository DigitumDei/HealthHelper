using HealthHelper.Pages;

namespace HealthHelper;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(MealDetailPage), typeof(MealDetailPage));
        Routing.RegisterRoute(nameof(DailySummaryPage), typeof(DailySummaryPage));
        Routing.RegisterRoute(nameof(ExerciseDetailPage), typeof(ExerciseDetailPage));
        Routing.RegisterRoute(nameof(ShareEntryPage), typeof(ShareEntryPage));
        Routing.RegisterRoute("week", typeof(WeekViewPage));
        Routing.RegisterRoute("month", typeof(MonthViewPage));
        Routing.RegisterRoute("year", typeof(YearViewPage));
        Routing.RegisterRoute("day", typeof(DayDetailPage));
    }
}
