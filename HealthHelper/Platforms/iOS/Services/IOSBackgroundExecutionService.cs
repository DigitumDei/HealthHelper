using HealthHelper.Services.Platform;

namespace HealthHelper.Platforms.iOS.Services;

/// <summary>
/// iOS implementation of IBackgroundExecutionService.
/// Currently a stub - full implementation will be added in Issue #34.
/// </summary>
public class IOSBackgroundExecutionService : IBackgroundExecutionService
{
    public void StartBackgroundTask(string taskName)
    {
        // TODO: Issue #34 - Implement iOS background task using UIApplication.BeginBackgroundTask
        // For now, do nothing - analysis will run but may be killed if app backgrounds
    }

    public void StopBackgroundTask(string taskName)
    {
        // TODO: Issue #34 - Implement iOS background task end using UIApplication.EndBackgroundTask
    }
}
