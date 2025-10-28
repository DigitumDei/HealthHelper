#if ANDROID
using Android;
using Android.Content.PM;
using AndroidX.Core.Content;

namespace HealthHelper.Platforms.Android.Permissions;

/// <summary>
/// Custom permission for POST_NOTIFICATIONS on Android 13+ (API 33+).
/// This permission is required to display foreground service notifications
/// that keep LLM analysis running when the screen is locked.
/// </summary>
public class PostNotificationsPermission : Microsoft.Maui.ApplicationModel.Permissions.BasePlatformPermission
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [(Manifest.Permission.PostNotifications, true)];

    public override bool ShouldShowRationale()
    {
        // Only relevant on Android 13+
        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return false;
        }

        var context = Platform.CurrentActivity ?? Platform.AppContext;
        if (context == null)
        {
            return false;
        }

        return ContextCompat.CheckSelfPermission(context, Manifest.Permission.PostNotifications)
            != Permission.Granted;
    }
}
#endif
