using System.Threading;
using System.Threading.Tasks;
using HealthHelper.Models;

namespace HealthHelper.Services.Share;

public interface ISharedImageImportService
{
    Task<SharedImageDraft> ImportAsync(Stream sourceStream, string? fileName, string? contentType, CancellationToken cancellationToken = default);
    Task<TrackedEntry> CommitAsync(Guid draftId, ShareEntryCommitRequest request, CancellationToken cancellationToken = default);
    void Discard(Guid draftId);
}

public sealed class ShareEntryCommitRequest
{
    public string EntryType { get; set; } = string.Empty;
    public string? Description { get; set; }
}
