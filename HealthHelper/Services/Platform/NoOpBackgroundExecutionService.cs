namespace HealthHelper.Services.Platform;

/// <summary>
/// No-op implementation of IBackgroundExecutionService for desktop platforms (Windows/Mac).
/// Desktop platforms don't have the same background execution restrictions as mobile platforms.
/// </summary>
public class NoOpBackgroundExecutionService : IBackgroundExecutionService
{
    public void StartBackgroundTask(string taskName)
    {
        // No action needed on desktop - no background restrictions
    }

    public void StopBackgroundTask(string taskName)
    {
        // No action needed on desktop - no background restrictions
    }
}
