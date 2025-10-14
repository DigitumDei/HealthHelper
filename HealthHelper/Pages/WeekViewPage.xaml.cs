using HealthHelper.PageModels;
using Microsoft.Extensions.DependencyInjection;

namespace HealthHelper.Pages;

public partial class WeekViewPage : ContentPage
{
    public WeekViewPage()
        : this(((App)Application.Current!).Services.GetRequiredService<WeekViewModel>())
    {
    }

    public WeekViewPage(WeekViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
