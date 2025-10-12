using HealthHelper.PageModels;
using Microsoft.Maui.Controls;

namespace HealthHelper.Pages;

public partial class ExerciseDetailPage : ContentPage
{
    public ExerciseDetailPage(ExerciseDetailViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
