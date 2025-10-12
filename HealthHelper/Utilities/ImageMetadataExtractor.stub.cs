#if !ANDROID && !IOS && !MACCATALYST
namespace HealthHelper.Utilities;

public static partial class ImageMetadataExtractor
{
    private static partial void FillPlatformMetadata(string absolutePath, PlatformMetadata metadata)
    {
        // Default implementation intentionally left blank for platforms without EXIF extraction support.
    }
}
#endif
