using HealthHelper.PageModels;

namespace HealthHelper.Pages;

public partial class MealDetailPage : ContentPage
{
	public MealDetailPage(MealDetailViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
