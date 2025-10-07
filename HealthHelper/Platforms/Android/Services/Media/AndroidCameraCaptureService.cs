using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Provider;
using Microsoft.Extensions.Logging;
using FileProvider = Microsoft.Maui.Storage.FileProvider;

namespace HealthHelper.Services.Media;

public sealed class AndroidCameraCaptureService : ICameraCaptureService
{
    private readonly ILogger<AndroidCameraCaptureService> _logger;

    public AndroidCameraCaptureService(ILogger<AndroidCameraCaptureService> logger)
    {
        _logger = logger;
    }

    public Task<CameraCaptureOutcome> CaptureAsync(PendingPhotoCapture capture, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);

        var activity = MainActivity.Instance;
        if (activity is null)
        {
            throw new InvalidOperationException("MainActivity instance is not available.");
        }

        var tcs = new TaskCompletionSource<CameraCaptureOutcome>();

        void OnActivityResult(object? sender, ActivityResultEventArgs e)
        {
            if (e.RequestCode != MainActivity.TakePhotoRequestCode)
            {
                return;
            }

            activity.ActivityResultReceived -= OnActivityResult;

            if (e.ResultCode == Result.Canceled)
            {
                _logger.LogInformation("Camera capture canceled by user.");
                SafeDeleteFile(capture.OriginalAbsolutePath);
                tcs.TrySetResult(CameraCaptureOutcome.Canceled());
                return;
            }

            if (e.ResultCode == Result.Ok)
            {
                _logger.LogInformation("Camera capture returned successfully.");
                tcs.TrySetResult(CameraCaptureOutcome.Success());
                return;
            }

            _logger.LogWarning("Camera capture failed with result code {ResultCode}.", e.ResultCode);
            SafeDeleteFile(capture.OriginalAbsolutePath);
            tcs.TrySetResult(CameraCaptureOutcome.Failed($"Camera returned result code {e.ResultCode}"));
        }

        activity.ActivityResultReceived += OnActivityResult;

        try
        {
            var parentDirectory = Path.GetDirectoryName(capture.OriginalAbsolutePath);
            if (string.IsNullOrEmpty(parentDirectory))
            {
                throw new InvalidOperationException("Original photo path is invalid.");
            }

            Directory.CreateDirectory(parentDirectory);

            if (File.Exists(capture.OriginalAbsolutePath))
            {
                File.Delete(capture.OriginalAbsolutePath);
            }

            using (File.Create(capture.OriginalAbsolutePath))
            {
                // Ensure the file exists for the camera app to write into.
            }

            var file = new Java.IO.File(capture.OriginalAbsolutePath);

            var authority = $"{activity.PackageName}.fileprovider";
            var photoUri = FileProvider.GetUriForFile(activity, authority, file);

            var intent = new Intent(MediaStore.ActionImageCapture);
            intent.PutExtra(MediaStore.ExtraOutput, photoUri);
            intent.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);

            GrantUriPermissionsForIntent(activity, intent, photoUri);

            activity.StartActivityForResult(intent, MainActivity.TakePhotoRequestCode);
        }
        catch (Exception ex)
        {
            activity.ActivityResultReceived -= OnActivityResult;
            SafeDeleteFile(capture.OriginalAbsolutePath);
            _logger.LogError(ex, "Failed to launch camera intent.");
            tcs.TrySetResult(CameraCaptureOutcome.Failed(ex.Message));
        }

        cancellationToken.Register(() =>
        {
            activity.ActivityResultReceived -= OnActivityResult;
            SafeDeleteFile(capture.OriginalAbsolutePath);
            tcs.TrySetCanceled(cancellationToken);
        });

        return tcs.Task;
    }

    private static void GrantUriPermissionsForIntent(Activity activity, Intent intent, Android.Net.Uri uri)
    {
        var resolveInfos = activity.PackageManager?.QueryIntentActivities(intent, PackageInfoFlags.MatchDefaultOnly);
        if (resolveInfos is null)
        {
            return;
        }

        foreach (var resolveInfo in resolveInfos.Where(info => info?.ActivityInfo?.PackageName is not null))
        {
            var packageName = resolveInfo.ActivityInfo!.PackageName!;
            activity.GrantUriPermission(packageName, uri, ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
        }
    }

    private static void SafeDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }
}
