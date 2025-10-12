using System.Collections.Concurrent;

namespace HealthHelper.Services.Share;

public sealed class InMemorySharedImageDraftStore : ISharedImageDraftStore
{
    private readonly ConcurrentDictionary<Guid, SharedImageDraft> _drafts = new();

    public void AddOrReplace(SharedImageDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        _drafts[draft.DraftId] = draft;
    }

    public SharedImageDraft? Get(Guid draftId)
    {
        _drafts.TryGetValue(draftId, out var draft);
        return draft;
    }

    public void Remove(Guid draftId)
    {
        _drafts.TryRemove(draftId, out _);
    }
}
