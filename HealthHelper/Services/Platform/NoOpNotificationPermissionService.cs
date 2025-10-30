namespace HealthHelper.Services.Platform;

/// <summary>
/// No-op implementation for platforms that don't require notification permissions
/// </summary>
public class NoOpNotificationPermissionService : INotificationPermissionService
{
    public Task EnsurePermissionAsync()
    {
        // No permission needed on non-Android platforms or Android < 13
        return Task.CompletedTask;
    }
}
