namespace HealthHelper.Services.Navigation;

public interface IHistoricalNavigationService
{
    Task NavigateToWeekAsync(DateTime? weekStart = null);

    Task NavigateToMonthAsync(int? year, int? month = null);

    Task NavigateToYearAsync(int? year = null);

    Task NavigateToDayAsync(DateTime date);

    Task NavigateBackAsync();

    Task NavigateToTodayAsync();
}
