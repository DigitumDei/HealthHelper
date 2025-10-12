using HealthHelper.PageModels;

namespace HealthHelper.Pages;

public partial class SleepDetailPage : ContentPage
{
    public SleepDetailPage(SleepDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
