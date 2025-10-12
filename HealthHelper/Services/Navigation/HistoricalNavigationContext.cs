using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HealthHelper.Services.Navigation;

/// <summary>
/// Tracks the currently displayed historical view and maintains the breadcrumb stack
/// used to restore the previous view when navigating backwards.
/// </summary>
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

    /// <summary>
    /// Gets a read-only view of the breadcrumb stack with the oldest item at index 0.
    /// </summary>
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

    /// <summary>
    /// Creates a breadcrumb for the current state so the caller can push it before changing levels.
    /// </summary>
    public NavigationBreadcrumb CreateCurrentBreadcrumb(string? label = null)
    {
        return new NavigationBreadcrumb(CurrentLevel, CurrentDate, label);
    }

    /// <summary>
    /// Pushes a breadcrumb representing the view the user is drilling down from.
    /// </summary>
    /// <remarks>The most recent breadcrumb is always the top of the stack.</remarks>
    public void PushBreadcrumb(NavigationBreadcrumb breadcrumb)
    {
        _breadcrumbStack.Add(breadcrumb);
        OnPropertyChanged(nameof(HasBreadcrumbs));
    }

    /// <summary>
    /// Pops the most recent breadcrumb when the user navigates back up the hierarchy.
    /// </summary>
    /// <returns>The previously active breadcrumb or <c>null</c> if the stack is empty.</returns>
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

    /// <summary>
    /// Updates the current navigation state without touching the breadcrumb stack.
    /// </summary>
    public void SetCurrent(HistoricalViewLevel level, DateTime date)
    {
        CurrentLevel = level;
        CurrentDate = date;
    }

    /// <summary>
    /// Clears the breadcrumb stack and sets the current view to the supplied values.
    /// </summary>
    public void Reset(HistoricalViewLevel level, DateTime date)
    {
        _breadcrumbStack.Clear();
        OnPropertyChanged(nameof(HasBreadcrumbs));
        SetCurrent(level, date);
    }
}
