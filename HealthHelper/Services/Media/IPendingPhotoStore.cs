using System.Threading;
using System.Threading.Tasks;

namespace HealthHelper.Services.Media;

public interface IPendingPhotoStore
{
    Task SaveAsync(PendingPhotoCapture capture, CancellationToken cancellationToken = default);
    Task<PendingPhotoCapture?> GetAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}
