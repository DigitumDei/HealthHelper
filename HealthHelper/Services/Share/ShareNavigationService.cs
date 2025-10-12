using System.Collections.Generic;
using HealthHelper.Pages;
using Microsoft.Maui.ApplicationModel;

namespace HealthHelper.Services.Share;

public sealed class ShareNavigationService : IShareNavigationService
{
    public async Task PresentShareDraftAsync(Guid draftId, CancellationToken cancellationToken = default)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var routeParameters = new Dictionary<string, object>
            {
                { "DraftId", draftId }
            };

            await Shell.Current.GoToAsync(nameof(ShareEntryPage), routeParameters).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }
}
