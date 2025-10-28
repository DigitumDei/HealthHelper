using System.Collections.Generic;
using HealthHelper.PageModels;
using Microsoft.Extensions.DependencyInjection;

namespace HealthHelper.Pages;

public partial class WeekViewPage : ContentPage, IQueryAttributable
{
    private readonly WeekViewModel _viewModel;

    public WeekViewPage()
        : this(((App)Application.Current!).Services.GetRequiredService<WeekViewModel>())
    {
    }

    public WeekViewPage(WeekViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _viewModel.TriggerInitialLoad();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _viewModel.ApplyQueryAttributes(query);
    }
}
