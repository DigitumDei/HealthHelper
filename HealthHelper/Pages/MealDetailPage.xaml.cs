using HealthHelper.PageModels;

namespace HealthHelper.Pages;

public partial class MealDetailPage : ContentPage
{
    private readonly MealDetailViewModel _viewModel;

    public MealDetailPage(MealDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.SubscribeToStatusChanges();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.UnsubscribeFromStatusChanges();
    }
}
