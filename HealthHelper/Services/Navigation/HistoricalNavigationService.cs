using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;

namespace HealthHelper.Services.Navigation;

public class HistoricalNavigationService : IHistoricalNavigationService
{
    private readonly HistoricalNavigationContext _navigationContext;
    private readonly ILogger<HistoricalNavigationService> _logger;
    private readonly SemaphoreSlim _navigationLock = new(1, 1);
    private static readonly TimeSpan NavigationLockTimeout = TimeSpan.FromSeconds(5);

    public HistoricalNavigationService(
        HistoricalNavigationContext navigationContext,
        ILogger<HistoricalNavigationService> logger)
    {
        _navigationContext = navigationContext;
        _logger = logger;
    }

    public Task NavigateToWeekAsync(DateTime? weekStart = null)
    {
        var targetDate = (weekStart ?? DateTime.Today).Date;
        return NavigateToLevelAsync(
            HistoricalViewLevel.Week,
            "week",
            targetDate,
            new Dictionary<string, object>
            {
                { "WeekStart", targetDate }
            });
    }

    public Task NavigateToMonthAsync(int? year, int? month = null)
    {
        var referenceDate = _navigationContext.CurrentDate;
        var targetYear = year ?? referenceDate.Year;
        var targetMonth = month ?? referenceDate.Month;
        if (targetMonth < 1 || targetMonth > 12)
        {
            targetMonth = Math.Clamp(targetMonth, 1, 12);
        }

        var targetDate = new DateTime(targetYear, targetMonth, 1);
        return NavigateToLevelAsync(
            HistoricalViewLevel.Month,
            "month",
            targetDate,
            new Dictionary<string, object>
            {
                { "Year", targetYear },
                { "Month", targetMonth }
            });
    }

    public Task NavigateToYearAsync(int? year = null)
    {
        var referenceDate = _navigationContext.CurrentDate;
        var targetYear = year ?? referenceDate.Year;
        if (targetYear < DateTime.MinValue.Year)
        {
            targetYear = DateTime.MinValue.Year;
        }
        else if (targetYear > DateTime.MaxValue.Year)
        {
            targetYear = DateTime.MaxValue.Year;
        }

        var targetDate = new DateTime(targetYear, 1, 1);
        return NavigateToLevelAsync(
            HistoricalViewLevel.Year,
            "year",
            targetDate,
            new Dictionary<string, object>
            {
                { "Year", targetYear }
            });
    }

    public Task NavigateToDayAsync(DateTime date)
    {
        var targetDate = date.Date;
        return NavigateToLevelAsync(
            HistoricalViewLevel.Day,
            "day",
            targetDate,
            new Dictionary<string, object>
            {
                { "Date", targetDate }
            });
    }

    public async Task NavigateBackAsync()
    {
        if (!await _navigationLock.WaitAsync(NavigationLockTimeout).ConfigureAwait(false))
        {
            _logger.LogWarning("Navigation lock timeout while navigating back.");
            return;
        }
        try
        {
            if (Shell.Current is null)
            {
                _logger.LogWarning("Shell.Current is null. Unable to navigate back.");
                return;
            }

            var previous = _navigationContext.PeekBreadcrumb();
            if (previous is null)
            {
                try
                {
                    await Shell.Current.GoToAsync("//today", true).ConfigureAwait(false);
                    _navigationContext.Reset(HistoricalViewLevel.Today, DateTime.Today);
                }
                catch (Exception navigationEx)
                {
                    _logger.LogError(navigationEx, "Failed to navigate back to today.");
                }

                return;
            }

            try
            {
                await Shell.Current.GoToAsync("..", true).ConfigureAwait(false);
                _navigationContext.PopBreadcrumb();
                _navigationContext.SetCurrent(previous.Level, previous.Date);
            }
            catch (Exception navigationEx)
            {
                _logger.LogError(navigationEx, "Failed to navigate back to previous breadcrumb.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate back.");
        }
        finally
        {
            _navigationLock.Release();
        }
    }

    public Task NavigateToTodayAsync()
    {
        return NavigateToLevelAsync(
            HistoricalViewLevel.Today,
            "today",
            DateTime.Today,
            parameters: null,
            pushCurrent: false,
            resetBreadcrumbs: true,
            useAbsoluteRoute: true);
    }

    private async Task NavigateToLevelAsync(
        HistoricalViewLevel targetLevel,
        string route,
        DateTime targetDate,
        IDictionary<string, object>? parameters,
        bool pushCurrent = true,
        bool resetBreadcrumbs = false,
        bool useAbsoluteRoute = false)
    {
        if (!await _navigationLock.WaitAsync(NavigationLockTimeout).ConfigureAwait(false))
        {
            _logger.LogWarning("Navigation lock timeout while navigating to {Route}.", route);
            return;
        }
        try
        {
            if (Shell.Current is null)
            {
                _logger.LogWarning("Shell.Current is null. Unable to navigate to {Route}.", route);
                return;
            }

            NavigationBreadcrumb? breadcrumbToPush = null;
            if (!resetBreadcrumbs && pushCurrent)
            {
                breadcrumbToPush = _navigationContext.CreateCurrentBreadcrumb();
            }

            var targetRoute = useAbsoluteRoute ? $"//{route}" : route;

            try
            {
                if (parameters is null || parameters.Count == 0)
                {
                    await Shell.Current.GoToAsync(targetRoute, true).ConfigureAwait(false);
                }
                else
                {
                    await Shell.Current.GoToAsync(targetRoute, true, parameters).ConfigureAwait(false);
                }
            }
            catch (Exception navigationEx)
            {
                _logger.LogError(navigationEx, "Failed to navigate to {Route}.", route);
                return;
            }

            if (resetBreadcrumbs)
            {
                _navigationContext.Reset(targetLevel, targetDate);
            }
            else
            {
                if (breadcrumbToPush is not null)
                {
                    _navigationContext.PushBreadcrumb(breadcrumbToPush);
                }

                _navigationContext.SetCurrent(targetLevel, targetDate);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to {Route}.", route);
        }
        finally
        {
            _navigationLock.Release();
        }
    }
}
