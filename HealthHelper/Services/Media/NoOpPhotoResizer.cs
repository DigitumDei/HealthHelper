using System.Threading;
using System.Threading.Tasks;

namespace HealthHelper.Services.Media;

public sealed class NoOpPhotoResizer : IPhotoResizer
{
    public Task ResizeAsync(string filePath, int maxWidth, int maxHeight, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
