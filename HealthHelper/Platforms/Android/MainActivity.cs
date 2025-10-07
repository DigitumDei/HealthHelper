using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;

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
