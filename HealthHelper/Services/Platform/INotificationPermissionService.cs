namespace HealthHelper.Services.Platform;

/// <summary>
/// Service for requesting notification permissions required for background analysis
/// </summary>
public interface INotificationPermissionService
{
    /// <summary>
    /// Request notification permission if needed (Android 13+)
    /// </summary>
    Task EnsurePermissionAsync();
}
