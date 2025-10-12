#if ANDROID
using Android.Content;
#endif

namespace HealthHelper.Services.Share;

public interface IShareIntentProcessor
{
#if ANDROID
    Task HandleAndroidShareAsync(Intent intent, CancellationToken cancellationToken = default);
#endif
}
