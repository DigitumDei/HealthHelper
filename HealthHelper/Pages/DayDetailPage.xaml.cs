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
            targetDate = value;
            UpdateDisplayedDate();
        }
    }

    private void UpdateDisplayedDate()
    {
        var displayDate = targetDate == default ? DateTime.Today : targetDate;
        DateLabel.Text = displayDate.ToString("D", CultureInfo.CurrentCulture);
    }
}
