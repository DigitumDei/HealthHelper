namespace HealthHelper.Services.Navigation;

/// <summary>
/// Provides Shell-based navigation entry points between the historical views of the app.
/// </summary>
public interface IHistoricalNavigationService
{
    /// <summary>
    /// Navigates to the week view, optionally targeting a specific week start date.
    /// </summary>
    /// <param name="weekStart">Optional start date of the target week. Defaults to today.</param>
    Task NavigateToWeekAsync(DateTime? weekStart = null);

    /// <summary>
    /// Navigates to the month view, optionally specifying year and month values.
    /// </summary>
    /// <param name="year">Optional year. Falls back to the currently active context.</param>
    /// <param name="month">Optional month. Falls back to the currently active context.</param>
    Task NavigateToMonthAsync(int? year, int? month = null);

    /// <summary>
    /// Navigates to the year view, optionally targeting a specific year.
    /// </summary>
    /// <param name="year">Optional year. Defaults to the current context year.</param>
    Task NavigateToYearAsync(int? year = null);

    /// <summary>
    /// Navigates to the day detail view for the provided date.
    /// </summary>
    /// <param name="date">The date whose details should be shown.</param>
    Task NavigateToDayAsync(DateTime date);

    /// <summary>
    /// Navigates to the previous historical view using the breadcrumb stack.
    /// </summary>
    Task NavigateBackAsync();

    /// <summary>
    /// Navigates directly to the "today" route and clears the breadcrumb stack.
    /// </summary>
    Task NavigateToTodayAsync();
}
