#if ANDROID
using HealthHelper.Services.Platform;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace HealthHelper.Platforms.Android.Services;

/// <summary>
/// Android implementation for requesting POST_NOTIFICATIONS permission
/// </summary>
public class AndroidNotificationPermissionService : INotificationPermissionService
{
    private readonly ILogger<AndroidNotificationPermissionService> _logger;

    public AndroidNotificationPermissionService(ILogger<AndroidNotificationPermissionService> logger)
    {
        _logger = logger;
    }

    public async Task EnsurePermissionAsync()
    {
        // Only needed on Android 13+ (API 33+)
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return;
        }

        var notificationStatus = await Microsoft.Maui.ApplicationModel.Permissions.CheckStatusAsync<HealthHelper.Platforms.Android.Permissions.PostNotificationsPermission>();
        if (notificationStatus == PermissionStatus.Granted)
        {
            return;
        }

        // Show rationale explaining why we need this permission
        if (Microsoft.Maui.ApplicationModel.Permissions.ShouldShowRationale<HealthHelper.Platforms.Android.Permissions.PostNotificationsPermission>())
        {
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await Shell.Current.DisplayAlertAsync(
                    "Background Analysis",
                    "We need notification permission to keep analyzing your photos even when the screen is locked. This ensures your meal analysis completes reliably.",
                    "OK");
            });
        }

        // Request the permission
        notificationStatus = await Microsoft.Maui.ApplicationModel.Permissions.RequestAsync<HealthHelper.Platforms.Android.Permissions.PostNotificationsPermission>();

        if (notificationStatus != PermissionStatus.Granted)
        {
            _logger.LogWarning("POST_NOTIFICATIONS permission denied. Background analysis may be interrupted if screen locks.");
        }
    }
}
#endif
