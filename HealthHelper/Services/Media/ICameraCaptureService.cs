using System.Threading;
using System.Threading.Tasks;

namespace HealthHelper.Services.Media;

public interface ICameraCaptureService
{
    Task<CameraCaptureOutcome> CaptureAsync(PendingPhotoCapture capture, CancellationToken cancellationToken = default);
}
