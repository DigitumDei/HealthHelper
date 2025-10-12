using System;
using System.Globalization;

namespace HealthHelper.Pages;

[QueryProperty(nameof(TargetDate), "Date")]
public partial class DayDetailPage : ContentPage
{
    private DateTime targetDate;

    public DayDetailPage()
    {
        InitializeComponent();
        UpdateDisplayedDate();
    }

    public DateTime TargetDate
    {
        get => targetDate;
        set
        {
            var normalized = value.Kind switch
            {
                DateTimeKind.Utc => value.ToLocalTime(),
                _ => value
            };

            // Normalize to date only to avoid time zone drift when navigating between contexts.
            targetDate = normalized.Date;
            UpdateDisplayedDate();
        }
    }

    private void UpdateDisplayedDate()
    {
        var displayDate = targetDate == default ? DateTime.Today : targetDate;
        DateLabel.Text = displayDate.ToString("D", CultureInfo.CurrentCulture);
    }
}
