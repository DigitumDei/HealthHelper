using Android.Content;
using Android.Content.PM;
using AndroidX.Core.Content;
using HealthHelper.Services.Platform;
using Microsoft.Maui.ApplicationModel;

namespace HealthHelper.Platforms.Android.Services;

/// <summary>
/// Android implementation of IBackgroundExecutionService.
/// Uses foreground service to ensure background tasks complete even when screen is locked.
/// </summary>
public class AndroidBackgroundExecutionService : IBackgroundExecutionService
{
    public void StartBackgroundTask(string taskName)
    {
        var context = Platform.CurrentActivity ?? Platform.AppContext;
        if (context == null)
        {
            return;
        }

        // Check if we have POST_NOTIFICATIONS permission on Android 13+
        bool hasNotificationPermission = true;
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            hasNotificationPermission = ContextCompat.CheckSelfPermission(
                context,
                global::Android.Manifest.Permission.PostNotifications) == Permission.Granted;
        }

        // Only start foreground service if we have notification permission
        // Otherwise, fall back to best-effort background execution
        if (!hasNotificationPermission)
        {
            // Without notification permission, we can't use a foreground service safely
            // The task will still run, but may be killed if the app is backgrounded for too long
            // This is better than crashing with SecurityException
            return;
        }

        var intent = new Intent(context, typeof(AnalysisForegroundService));
        intent.PutExtra("taskName", taskName);

        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            context.StartForegroundService(intent);
        }
        else
        {
            context.StartService(intent);
        }
    }

    public void StopBackgroundTask(string taskName)
    {
        var context = Platform.CurrentActivity ?? Platform.AppContext;
        if (context == null)
        {
            return;
        }

        // Decrement the task count; service will stop itself if count reaches 0
        AnalysisForegroundService.DecrementTaskCount(context);
    }
}
