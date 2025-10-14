using HealthHelper.PageModels;

namespace HealthHelper.Pages;

public partial class WeekViewPage : ContentPage
{
    public WeekViewPage(WeekViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
