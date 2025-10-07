using System.Threading;
using System.Threading.Tasks;

namespace HealthHelper.Services.Media;

public interface IPhotoResizer
{
    Task ResizeAsync(string filePath, int maxWidth, int maxHeight, CancellationToken cancellationToken = default);
}
