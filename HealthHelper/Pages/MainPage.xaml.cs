using HealthHelper.Models;
using HealthHelper.PageModels;

namespace HealthHelper.Pages;

public partial class MainPage : ContentPage
{
	public MainPage(MainPageModel model)
	{
		InitializeComponent();
		BindingContext = model;
	}
}