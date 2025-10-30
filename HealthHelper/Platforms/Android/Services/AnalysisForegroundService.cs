using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace HealthHelper.Platforms.Android.Services;

/// <summary>
/// Android foreground service that ensures LLM analysis completes even when
/// the screen is locked or the app is backgrounded.
/// Shows a persistent notification while analysis is running.
/// </summary>
[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeDataSync)]
public class AnalysisForegroundService : Service
{
    private const string ChannelId = "analysis_channel";
    private const string ChannelName = "Analysis";
    private const int NotificationId = 1001;
    private static readonly object _lock = new object();
    private static int _activeTaskCount = 0;

    public override IBinder? OnBind(Intent? intent)
    {
        // Not a bound service
        return null;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        lock (_lock)
        {
            _activeTaskCount++;

            // Create notification channel (required for Android 8+)
            CreateNotificationChannel();

            // Build and display notification
            var notification = BuildNotification();
            StartForeground(NotificationId, notification);
        }

        // Return Sticky so service is restarted if killed by system
        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        lock (_lock)
        {
            _activeTaskCount--;

            if (_activeTaskCount <= 0)
            {
                _activeTaskCount = 0;
                if (OperatingSystem.IsAndroidVersionAtLeast(24))
                {
                    StopForeground(StopForegroundFlags.Remove);
                }
                else
                {
#pragma warning disable CS0618 // StopForeground(bool) is obsolete but required for API < 24
                    StopForeground(true);
#pragma warning restore CS0618
                }
            }
        }

        base.OnDestroy();
    }

    /// <summary>
    /// Decrements the task count and stops the service if no tasks remain.
    /// Called by AndroidBackgroundExecutionService when a task completes.
    /// </summary>
    public static void DecrementTaskCount(Context context)
    {
        lock (_lock)
        {
            _activeTaskCount--;

            if (_activeTaskCount <= 0)
            {
                _activeTaskCount = 0;
                var intent = new Intent(context, typeof(AnalysisForegroundService));
                context.StopService(intent);
            }
        }
    }

    private void CreateNotificationChannel()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var channel = new NotificationChannel(
                ChannelId,
                ChannelName,
                NotificationImportance.Low) // Low = no sound/vibration
            {
                Description = "Shows when analyzing meal photos"
            };

            var notificationManager = GetSystemService(NotificationService) as NotificationManager;
            notificationManager?.CreateNotificationChannel(channel);
        }
    }

    private Notification BuildNotification()
    {
        // Create intent to open app when notification is tapped
        var pendingIntentFlags = OperatingSystem.IsAndroidVersionAtLeast(23)
            ? PendingIntentFlags.Immutable
            : PendingIntentFlags.UpdateCurrent;

        var pendingIntent = PendingIntent.GetActivity(
            this,
            0,
            PackageManager?.GetLaunchIntentForPackage(PackageName ?? string.Empty),
            pendingIntentFlags);

        var taskCountText = _activeTaskCount == 1
            ? "Processing 1 entry..."
            : $"Processing {_activeTaskCount} entries...";

        var builder = new NotificationCompat.Builder(this, ChannelId)!
            .SetContentTitle("Analyzing your entries")!
            .SetSmallIcon(global::Android.Resource.Drawable.IcMenuUpload)! // Using system icon for now
            .SetOngoing(true)! // Makes it persistent
            .SetPriority(NotificationCompat.PriorityLow)!; // Low priority = no sound

        if (pendingIntent != null)
        {
            builder.SetContentIntent(pendingIntent);
        }

        if (taskCountText != null)
        {
            builder.SetContentText(taskCountText);
        }

        return builder.Build()!;
    }
}
