using HealthHelper.PageModels;
using Microsoft.Maui.Controls;

namespace HealthHelper.Pages;

public partial class ShareEntryPage : ContentPage
{
    public ShareEntryPage(ShareEntryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
