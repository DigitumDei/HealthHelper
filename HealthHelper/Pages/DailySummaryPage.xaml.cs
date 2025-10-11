using HealthHelper.PageModels;

namespace HealthHelper.Pages;

public partial class DailySummaryPage : ContentPage
{
    public DailySummaryPage(DailySummaryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
