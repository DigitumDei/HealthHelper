using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HealthHelper.Services.Navigation;

public partial class HistoricalNavigationContext : ObservableObject
{
    private readonly ObservableCollection<NavigationBreadcrumb> _breadcrumbStack = new();
    private HistoricalViewLevel currentLevel;
    private DateTime currentDate;

    public HistoricalNavigationContext()
    {
        currentLevel = HistoricalViewLevel.Today;
        currentDate = DateTime.Today;
        Breadcrumbs = new ReadOnlyObservableCollection<NavigationBreadcrumb>(_breadcrumbStack);
    }

    public ReadOnlyObservableCollection<NavigationBreadcrumb> Breadcrumbs { get; }

    public HistoricalViewLevel CurrentLevel
    {
        get => currentLevel;
        private set => SetProperty(ref currentLevel, value);
    }

    public DateTime CurrentDate
    {
        get => currentDate;
        private set => SetProperty(ref currentDate, value);
    }

    public bool HasBreadcrumbs => _breadcrumbStack.Count > 0;

    public NavigationBreadcrumb CreateCurrentBreadcrumb(string? label = null)
    {
        return new NavigationBreadcrumb(CurrentLevel, CurrentDate, label);
    }

    public void PushBreadcrumb(NavigationBreadcrumb breadcrumb)
    {
        _breadcrumbStack.Add(breadcrumb);
        OnPropertyChanged(nameof(HasBreadcrumbs));
    }

    public NavigationBreadcrumb? PopBreadcrumb()
    {
        if (_breadcrumbStack.Count == 0)
        {
            return null;
        }

        var index = _breadcrumbStack.Count - 1;
        var breadcrumb = _breadcrumbStack[index];
        _breadcrumbStack.RemoveAt(index);
        OnPropertyChanged(nameof(HasBreadcrumbs));
        return breadcrumb;
    }

    public void SetCurrent(HistoricalViewLevel level, DateTime date)
    {
        CurrentLevel = level;
        CurrentDate = date;
    }

    public void Reset(HistoricalViewLevel level, DateTime date)
    {
        _breadcrumbStack.Clear();
        OnPropertyChanged(nameof(HasBreadcrumbs));
        SetCurrent(level, date);
    }
}
