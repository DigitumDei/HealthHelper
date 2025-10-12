using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using HealthHelper.Services.Share;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;

namespace HealthHelper;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges = ConfigChanges.ScreenSize |
                           ConfigChanges.Orientation |
                           ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout |
                           ConfigChanges.SmallestScreenSize |
                           ConfigChanges.Density |
                           ConfigChanges.KeyboardHidden |
                           ConfigChanges.LayoutDirection)]
public class MainActivity : MauiAppCompatActivity
{
    internal const int TakePhotoRequestCode = 9001;

    internal static MainActivity? Instance { get; private set; }

    internal event EventHandler<ActivityResultEventArgs>? ActivityResultReceived;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Instance = this;
        ProcessShareIntent(Intent);
    }

    protected override void OnDestroy()
    {
        if (ReferenceEquals(Instance, this))
        {
            Instance = null;
        }

        base.OnDestroy();
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);
        ActivityResultReceived?.Invoke(this, new ActivityResultEventArgs(requestCode, resultCode, data));
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        ProcessShareIntent(intent);
    }

    private void ProcessShareIntent(Intent? intent)
    {
        if (intent is null)
        {
            return;
        }

        if (Microsoft.Maui.Controls.Application.Current is not App app)
        {
            return;
        }

        using var scope = app.Services.CreateScope();
        var processor = scope.ServiceProvider.GetService<IShareIntentProcessor>();
        if (processor is null)
        {
            return;
        }

        var clonedIntent = new Intent(intent);
        _ = processor.HandleAndroidShareAsync(clonedIntent);
    }
}

public sealed class ActivityResultEventArgs : EventArgs
{
    public ActivityResultEventArgs(int requestCode, Result resultCode, Intent? data)
    {
        RequestCode = requestCode;
        ResultCode = resultCode;
        Data = data;
    }

    public int RequestCode { get; }
    public Result ResultCode { get; }
    public Intent? Data { get; }
}
