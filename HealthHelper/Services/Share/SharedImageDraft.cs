using System.IO;
using HealthHelper.Utilities;
using Microsoft.Maui.Storage;

namespace HealthHelper.Services.Share;

public sealed class SharedImageDraft
{
    public SharedImageDraft(
        Guid draftId,
        string originalRelativePath,
        string previewRelativePath,
        SharedImageMetadata metadata,
        string? originalFileName,
        string? contentType)
    {
        DraftId = draftId;
        OriginalRelativePath = originalRelativePath;
        PreviewRelativePath = previewRelativePath;
        Metadata = metadata;
        OriginalFileName = originalFileName;
        ContentType = contentType;
    }

    public Guid DraftId { get; }
    public string OriginalRelativePath { get; }
    public string PreviewRelativePath { get; }
    public SharedImageMetadata Metadata { get; }
    public string? OriginalFileName { get; }
    public string? ContentType { get; }

    public string OriginalAbsolutePath => Path.Combine(FileSystem.AppDataDirectory, OriginalRelativePath);
    public string PreviewAbsolutePath => Path.Combine(FileSystem.AppDataDirectory, PreviewRelativePath);
}
