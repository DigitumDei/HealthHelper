using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Services.Analysis;
using HealthHelper.Services.Navigation;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;

namespace HealthHelper.PageModels;

public partial class WeekViewModel : ObservableObject, IQueryAttributable
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly IHistoricalNavigationService _navigationService;
    private readonly HistoricalNavigationContext _navigationContext;
    private readonly WeekSummaryBuilder _summaryBuilder;
    private readonly ILogger<WeekViewModel> _logger;
    private readonly TimeZoneInfo _displayTimeZone;
    private readonly DateTime _currentWeekStart;
    private bool _hasLoadedOnce;

    public WeekViewModel(
        ITrackedEntryRepository trackedEntryRepository,
        WeekSummaryBuilder summaryBuilder,
        IHistoricalNavigationService navigationService,
        HistoricalNavigationContext navigationContext,
        ILogger<WeekViewModel> logger)
    {
        _trackedEntryRepository = trackedEntryRepository;
        _navigationService = navigationService;
        _navigationContext = navigationContext;
        _summaryBuilder = summaryBuilder;
        _logger = logger;
        _displayTimeZone = TimeZoneInfo.Local;

        Days = new ObservableCollection<WeekDayView>();
        _currentWeekStart = NormalizeToWeekStart(DateTime.Today);

        WeekStartDate = _currentWeekStart;
        WeekRangeDisplay = BuildWeekRangeLabel(WeekStartDate);
        IsDrilledDown = false;
    }

    public ObservableCollection<WeekDayView> Days { get; }

    [ObservableProperty]
    private DateTime weekStartDate;

    [ObservableProperty]
    private string weekRangeDisplay = string.Empty;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isWeeklySummaryLoading;

    [ObservableProperty]
    private bool showEmptyState;

    [ObservableProperty]
    private bool isDrilledDown;

    [ObservableProperty]
    private DailySummary? weeklySummary;

    [ObservableProperty]
    private string weeklySummaryMessage = string.Empty;

    partial void OnWeekStartDateChanged(DateTime value)
    {
        WeekRangeDisplay = BuildWeekRangeLabel(value);
        GoToNextWeekCommand.NotifyCanExecuteChanged();
    }

    public bool IsCurrentWeek => WeekStartDate == _currentWeekStart;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("WeekStart", out var parameter))
        {
            WeekStartDate = NormalizeToWeekStart(ParseDate(parameter));
        }

        TriggerInitialLoad();
    }

    public void TriggerInitialLoad()
    {
        var shouldReload = RestoreWeekSelectionFromContext();

        if (_hasLoadedOnce)
        {
            if (shouldReload && !IsLoading)
            {
                _ = LoadWeekAsync();
            }

            return;
        }

        if (IsLoading)
        {
            return;
        }

        if (shouldReload)
        {
            _ = LoadWeekAsync();
            return;
        }

        _ = LoadWeekAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadWeekAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task SelectDayAsync(WeekDayView? day)
    {
        if (day is null)
        {
            return;
        }

        try
        {
            await _navigationService.NavigateToDayAsync(day.Date).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to day view for {Date}.", day.Date);
        }
    }

    [RelayCommand]
    private async Task GoToPreviousWeekAsync()
    {
        WeekStartDate = WeekStartDate.AddDays(-7);
        await LoadWeekAsync().ConfigureAwait(false);
    }

    [RelayCommand(CanExecute = nameof(CanNavigateToNextWeek))]
    private async Task GoToNextWeekAsync()
    {
        WeekStartDate = WeekStartDate.AddDays(7);
        await LoadWeekAsync().ConfigureAwait(false);
    }

    private bool CanNavigateToNextWeek() => WeekStartDate < _currentWeekStart;

    [RelayCommand]
    private async Task SwipeLeftAsync()
    {
        try
        {
            await _navigationService.NavigateToMonthAsync(WeekStartDate.Year, WeekStartDate.Month).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to month view from week view.");
        }
    }

    [RelayCommand]
    private async Task SwipeRightAsync()
    {
        try
        {
            if (IsDrilledDown && _navigationContext.PeekBreadcrumb()?.Level == HistoricalViewLevel.Month)
            {
                await _navigationService.NavigateBackAsync().ConfigureAwait(false);
                return;
            }

            await _navigationService.NavigateToTodayAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate from week view via swipe right gesture.");
        }
    }

    private async Task LoadWeekAsync()
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            WeeklySummaryMessage = string.Empty;

            var summaries = await _trackedEntryRepository
                .GetDaySummariesForWeekAsync(WeekStartDate, _displayTimeZone);

            var summaryList = summaries ?? Array.Empty<DaySummary>();
            var dayViews = summaryList.Select(MapToDayView).ToList();

            Days.Clear();
            foreach (var day in dayViews)
            {
                Days.Add(day);
            }

            ShowEmptyState = Days.All(d => d.TotalCount == 0);

            UpdateWeekContext();

            await LoadWeeklySummaryAsync(summaryList).ConfigureAwait(false);
            _hasLoadedOnce = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load week view for start date {WeekStart}.", WeekStartDate);
            WeeklySummaryMessage = "We couldn't load this week's data. Pull to refresh or try again later.";
            Days.Clear();
            ShowEmptyState = true;
            WeeklySummary = null;
        }
        finally
        {
            IsLoading = false;
            GoToNextWeekCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task LoadWeeklySummaryAsync(IReadOnlyList<DaySummary> daySummaries)
    {
        try
        {
            IsWeeklySummaryLoading = true;
            var weeklySummary = await _summaryBuilder.BuildAsync(WeekStartDate, daySummaries);
            WeeklySummary = weeklySummary;
            WeeklySummaryMessage = weeklySummary is null
                ? "Weekly summary not available yet."
                : string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build weekly summary for {WeekStart}.", WeekStartDate);
            WeeklySummaryMessage = "We couldn't build the weekly summary.";
            WeeklySummary = null;
        }
        finally
        {
            IsWeeklySummaryLoading = false;
        }
    }

    private WeekDayView MapToDayView(DaySummary summary)
    {
        var dayDate = summary.Date;
        var dayName = dayDate.ToString("ddd", CultureInfo.CurrentCulture);
        var dayNumber = dayDate.ToString("M", CultureInfo.CurrentCulture);
        var previewPaths = summary.Previews
            .Select(preview => ResolvePreviewPath(preview.RelativePath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToList();

        return new WeekDayView(
            dayDate,
            $"{dayName}",
            dayNumber,
            summary.MealCount,
            summary.ExerciseCount,
            summary.SleepCount,
            summary.OtherCount,
            summary.PendingCount,
            summary.TotalCount,
            summary.HasDailySummary,
            summary.DailySummaryStatus,
            summary.DailySummaryEntryId,
            previewPaths,
            dayDate.Date == DateTime.Today);
    }

    private void UpdateWeekContext()
    {
        _navigationContext.SetCurrent(HistoricalViewLevel.Week, WeekStartDate);
        IsDrilledDown = _navigationContext.PeekBreadcrumb()?.Level == HistoricalViewLevel.Month;
        WeekRangeDisplay = BuildWeekRangeLabel(WeekStartDate);
    }

    private bool RestoreWeekSelectionFromContext()
    {
        var requiresReload = false;

        if (_navigationContext.CurrentLevel == HistoricalViewLevel.Day)
        {
            var breadcrumb = _navigationContext.PeekBreadcrumb();
            if (breadcrumb?.Level == HistoricalViewLevel.Week)
            {
                _navigationContext.PopBreadcrumb();
                var normalizedWeek = NormalizeToWeekStart(breadcrumb.Date);
                requiresReload = NormalizeWeekSelection(normalizedWeek);
                _navigationContext.SetCurrent(HistoricalViewLevel.Week, normalizedWeek);
                IsDrilledDown = _navigationContext.PeekBreadcrumb()?.Level == HistoricalViewLevel.Month;
                return requiresReload;
            }
        }

        if (_navigationContext.CurrentLevel == HistoricalViewLevel.Week)
        {
            var normalizedWeek = NormalizeToWeekStart(_navigationContext.CurrentDate);
            requiresReload = NormalizeWeekSelection(normalizedWeek);
            _navigationContext.SetCurrent(HistoricalViewLevel.Week, normalizedWeek);
            IsDrilledDown = _navigationContext.PeekBreadcrumb()?.Level == HistoricalViewLevel.Month;
            return requiresReload;
        }

        IsDrilledDown = _navigationContext.PeekBreadcrumb()?.Level == HistoricalViewLevel.Month;
        return requiresReload;
    }

    private bool NormalizeWeekSelection(DateTime normalizedWeek)
    {
        if (WeekStartDate == normalizedWeek)
        {
            return false;
        }

        WeekStartDate = normalizedWeek;
        return true;
    }

    private static DateTime ParseDate(object value)
    {
        return value switch
        {
            DateTime dt => dt,
            string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed) => parsed,
            _ => DateTime.Today
        };
    }

    private static DateTime NormalizeToWeekStart(DateTime value)
    {
        var target = value.Date;
        var firstDay = CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;
        var diff = (7 + (target.DayOfWeek - firstDay)) % 7;
        return target.AddDays(-diff);
    }

    private static string BuildWeekRangeLabel(DateTime weekStart)
    {
        var weekEnd = weekStart.AddDays(6);
        if (weekStart.Month == weekEnd.Month)
        {
            return $"{weekStart:MMM d} – {weekEnd:d}, {weekEnd:yyyy}";
        }

        return $"{weekStart:MMM d} – {weekEnd:MMM d}, {weekEnd:yyyy}";
    }

    private static string? ResolvePreviewPath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }
#if UNIT_TESTS
        return Path.Combine(AppContext.BaseDirectory, relativePath);
#else
        return Path.Combine(FileSystem.AppDataDirectory, relativePath);
#endif
    }
}

public class WeekDayView
{
    public WeekDayView(
        DateTime date,
        string dayLabel,
        string dateLabel,
        int mealCount,
        int exerciseCount,
        int sleepCount,
        int otherCount,
        int pendingCount,
        int totalCount,
        bool hasDailySummary,
        ProcessingStatus? dailySummaryStatus,
        int? dailySummaryEntryId,
        IReadOnlyList<string> previewImagePaths,
        bool isToday)
    {
        Date = date;
        DayLabel = dayLabel;
        DateLabel = dateLabel;
        MealCount = mealCount;
        ExerciseCount = exerciseCount;
        SleepCount = sleepCount;
        OtherCount = otherCount;
        PendingCount = pendingCount;
        TotalCount = totalCount;
        HasDailySummary = hasDailySummary;
        DailySummaryStatus = dailySummaryStatus;
        DailySummaryEntryId = dailySummaryEntryId;
        PreviewImagePaths = previewImagePaths;
        IsToday = isToday;
    }

    public DateTime Date { get; }
    public string DayLabel { get; }
    public string DateLabel { get; }
    public int MealCount { get; }
    public int ExerciseCount { get; }
    public int SleepCount { get; }
    public int OtherCount { get; }
    public int PendingCount { get; }
    public int TotalCount { get; }
    public bool HasDailySummary { get; }
    public ProcessingStatus? DailySummaryStatus { get; }
    public int? DailySummaryEntryId { get; }
    public IReadOnlyList<string> PreviewImagePaths { get; }
    public bool IsToday { get; }

    public bool HasEntries => TotalCount > 0;

    public string SummaryStatusText => DailySummaryStatus switch
    {
        ProcessingStatus.Completed => "Summary ready",
        ProcessingStatus.Pending => "Summary pending",
        ProcessingStatus.Processing => "Summary processing",
        ProcessingStatus.Failed => "Summary failed",
        ProcessingStatus.Skipped => "Summary skipped",
        _ when HasDailySummary => "Summary unavailable",
        _ => "Summary not generated"
    };
}
