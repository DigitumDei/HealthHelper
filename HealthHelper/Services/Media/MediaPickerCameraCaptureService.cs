using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;

namespace HealthHelper.Services.Media;

public sealed class MediaPickerCameraCaptureService : ICameraCaptureService
{
    public async Task<CameraCaptureOutcome> CaptureAsync(PendingPhotoCapture capture, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);

        try
        {
            var photo = await MediaPicker.Default.CapturePhotoAsync().ConfigureAwait(false);
            if (photo is null)
            {
                return CameraCaptureOutcome.Canceled();
            }

            Directory.CreateDirectory(Path.GetDirectoryName(capture.OriginalAbsolutePath)!);

            await using (var sourceStream = await photo.OpenReadAsync().ConfigureAwait(false))
            {
                await using var destinationStream = File.Create(capture.OriginalAbsolutePath);
                await sourceStream.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);
            }

            return CameraCaptureOutcome.Success();
        }
        catch (FeatureNotSupportedException)
        {
            return CameraCaptureOutcome.Failed("Camera capture is not supported on this device.");
        }
        catch (PermissionException)
        {
            return CameraCaptureOutcome.Failed("Camera permission is required.");
        }
        catch (OperationCanceledException)
        {
            return CameraCaptureOutcome.Canceled();
        }
        catch (Exception ex)
        {
            return CameraCaptureOutcome.Failed(ex.Message);
        }
    }
}
