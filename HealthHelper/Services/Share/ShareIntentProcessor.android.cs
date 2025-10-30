#if ANDROID
using System.IO;
using Android.Content;
using Android.Database;
using Android.Provider;
using HealthHelper.Utilities;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.Logging;
using Java.Lang;

namespace HealthHelper.Services.Share;

public sealed class ShareIntentProcessor : IShareIntentProcessor
{
    private readonly ISharedImageImportService _sharedImageImportService;
    private readonly IShareNavigationService _shareNavigationService;
    private readonly ILogger<ShareIntentProcessor> _logger;

    public ShareIntentProcessor(
        ISharedImageImportService sharedImageImportService,
        IShareNavigationService shareNavigationService,
        ILogger<ShareIntentProcessor> logger)
    {
        _sharedImageImportService = sharedImageImportService;
        _shareNavigationService = shareNavigationService;
        _logger = logger;
    }

    public async Task HandleAndroidShareAsync(Intent intent, CancellationToken cancellationToken = default)
    {
        if (intent is null)
        {
            return;
        }

        if (!string.Equals(intent.Action, Intent.ActionSend, StringComparison.Ordinal))
        {
            return;
        }

        if (intent.Type is null || !intent.Type.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Ignoring share intent with unsupported MIME type {Type}.", intent.Type);
            return;
        }

        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity ?? throw new InvalidOperationException("No current activity available to handle share intent.");
        var resolver = activity.ContentResolver ?? throw new InvalidOperationException("Content resolver not available.");

        var sharedUri = GetShareUri(intent);
        if (sharedUri is null)
        {
            _logger.LogWarning("Share intent received without a data stream.");
            return;
        }

        await using var stream = resolver.OpenInputStream(sharedUri);
        if (stream is null)
        {
            _logger.LogWarning("Unable to open input stream for shared content {Uri}.", sharedUri);
            return;
        }

        var contentType = resolver.GetType(sharedUri) ?? intent.Type;
        var fileName = TryResolveDisplayName(resolver, sharedUri) ?? BuildFallbackFileName(contentType);

        try
        {
            var draft = await _sharedImageImportService.ImportAsync(stream, fileName, contentType, cancellationToken).ConfigureAwait(false);

            if (draft.Metadata.IsLikelyScreenshot)
            {
                _logger.LogInformation("Imported shared screenshot {DraftId}.", draft.DraftId);
            }
            else if (draft.Metadata.HasExifTimestamp)
            {
                _logger.LogInformation("Imported shared photo {DraftId} with EXIF timestamp {Timestamp}.", draft.DraftId, draft.Metadata.CapturedAtUtc);
            }

            await _shareNavigationService.PresentShareDraftAsync(draft.DraftId, cancellationToken).ConfigureAwait(false);
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to import shared content.");
        }
        finally
        {
            intent.RemoveExtra(Intent.ExtraStream);
        }
    }

    private static Android.Net.Uri? GetShareUri(Intent intent)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            if (intent.GetParcelableExtra(Intent.ExtraStream, Class.FromType(typeof(Android.Net.Uri))) is Android.Net.Uri typedUri)
            {
                return typedUri;
            }
        }
        else
        {
#pragma warning disable CS0618 // GetParcelableExtra(string) is obsolete but required for older API levels
            if (intent.GetParcelableExtra(Intent.ExtraStream) is Android.Net.Uri legacyUri)
            {
                return legacyUri;
            }
#pragma warning restore CS0618
        }

        if (intent.ClipData?.ItemCount > 0)
        {
            return intent.ClipData.GetItemAt(0)?.Uri;
        }

        return null;
    }

    private static string? TryResolveDisplayName(ContentResolver resolver, Android.Net.Uri uri)
    {
        var column = DocumentsContract.Document.ColumnDisplayName;
        using var cursor = resolver.Query(uri, new[] { column }, null, null, null);
        if (cursor is null)
        {
            return null;
        }

        if (!cursor.MoveToFirst())
        {
            return null;
        }

        var index = cursor.GetColumnIndex(column);
        if (index < 0)
        {
#pragma warning disable CS0618 // OpenableColumns is obsolete on newer API levels
            index = cursor.GetColumnIndex(OpenableColumns.DisplayName);
#pragma warning restore CS0618
            if (index < 0)
            {
                return null;
            }
        }

        return cursor.GetString(index);
    }

    private static string BuildFallbackFileName(string? contentType)
    {
        var extension = contentType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/heic" => ".heic",
            "image/heif" => ".heif",
            "image/gif" => ".gif",
            _ => ".jpg"
        };

        return $"shared_{Guid.NewGuid():N}{extension}";
    }
}
#endif
