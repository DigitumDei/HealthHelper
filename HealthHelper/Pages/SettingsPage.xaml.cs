using CommunityToolkit.Mvvm.Input;
using HealthHelper.PageModels;

namespace HealthHelper.Pages;

public partial class SettingsPage : ContentPage
{
	public SettingsPage(SettingsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();

		if (BindingContext is SettingsViewModel viewModel)
		{
			await viewModel.LoadSettingsCommand.ExecuteAsync(null);
		}
	}
}
