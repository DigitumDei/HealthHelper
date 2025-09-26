using HealthHelper.Pages;

namespace HealthHelper;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        Routing.RegisterRoute(nameof(SettingsPage), typeof(SettingsPage));
        Routing.RegisterRoute(nameof(MealDetailPage), typeof(MealDetailPage));
    }
}
