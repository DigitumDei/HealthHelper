using System.IO;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Services.Analysis;
using HealthHelper.Services.Media;
using HealthHelper.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

namespace HealthHelper.Services.Share;

public sealed class SharedImageImportService : ISharedImageImportService
{
    private readonly IPhotoResizer _photoResizer;
    private readonly ISharedImageDraftStore _draftStore;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IBackgroundAnalysisService _backgroundAnalysisService;
    private readonly ILogger<SharedImageImportService> _logger;

    public SharedImageImportService(
        IPhotoResizer photoResizer,
        ISharedImageDraftStore draftStore,
        IServiceScopeFactory scopeFactory,
        IBackgroundAnalysisService backgroundAnalysisService,
        ILogger<SharedImageImportService> logger)
    {
        _photoResizer = photoResizer;
        _draftStore = draftStore;
        _scopeFactory = scopeFactory;
        _backgroundAnalysisService = backgroundAnalysisService;
        _logger = logger;
    }

    public async Task<SharedImageDraft> ImportAsync(Stream sourceStream, string? fileName, string? contentType, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);

        var draftId = Guid.NewGuid();
        var extension = ResolveExtension(fileName, contentType);

        var relativeDirectory = Path.Combine("Shares", "Pending");
        var directory = Path.Combine(FileSystem.AppDataDirectory, relativeDirectory);
        Directory.CreateDirectory(directory);

        var originalRelative = Path.Combine(relativeDirectory, $"{draftId:N}_original{extension}");
        var previewRelative = Path.Combine(relativeDirectory, $"{draftId:N}_preview{extension}");

        var originalAbsolute = Path.Combine(FileSystem.AppDataDirectory, originalRelative);
        var previewAbsolute = Path.Combine(FileSystem.AppDataDirectory, previewRelative);

        await using (var destination = File.Create(originalAbsolute))
        {
            await sourceStream.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        File.Copy(originalAbsolute, previewAbsolute, overwrite: true);
        await _photoResizer.ResizeAsync(previewAbsolute, 1280, 1280, cancellationToken).ConfigureAwait(false);

        var metadata = ImageMetadataExtractor.Extract(originalAbsolute);

        var draft = new SharedImageDraft(draftId, originalRelative, previewRelative, metadata, fileName, contentType);
        _draftStore.AddOrReplace(draft);

        return draft;
    }

    public async Task<TrackedEntry> CommitAsync(Guid draftId, ShareEntryCommitRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var draft = _draftStore.Get(draftId);
        if (draft is null)
        {
            throw new InvalidOperationException($"Draft {draftId} was not found or has already been processed.");
        }

        var entryGuid = Guid.NewGuid();
        var relativeDirectory = Path.Combine("Entries", "Unknown");
        var directory = Path.Combine(FileSystem.AppDataDirectory, relativeDirectory);
        Directory.CreateDirectory(directory);

        var originalExtension = Path.GetExtension(draft.OriginalAbsolutePath);
        if (string.IsNullOrWhiteSpace(originalExtension))
        {
            originalExtension = ".jpg";
        }

        var previewExtension = Path.GetExtension(draft.PreviewAbsolutePath);
        if (string.IsNullOrWhiteSpace(previewExtension))
        {
            previewExtension = ".jpg";
        }

        var originalRelativePath = Path.Combine(relativeDirectory, $"{entryGuid:N}{originalExtension}");
        var previewRelativePath = Path.Combine(relativeDirectory, $"{entryGuid:N}_preview{previewExtension}");

        var originalDestination = Path.Combine(FileSystem.AppDataDirectory, originalRelativePath);
        var previewDestination = Path.Combine(FileSystem.AppDataDirectory, previewRelativePath);

        File.Move(draft.OriginalAbsolutePath, originalDestination, overwrite: true);
        File.Move(draft.PreviewAbsolutePath, previewDestination, overwrite: true);

        var capturedAtUtc = EnsureUtc(draft.Metadata.CapturedAtUtc);
        var (timeZoneId, offsetMinutes) = ResolveTimeZoneMetadata(draft.Metadata, capturedAtUtc);

        var newEntry = new TrackedEntry
        {
            EntryType = EntryType.Unknown,
            CapturedAt = capturedAtUtc,
            CapturedAtTimeZoneId = timeZoneId,
            CapturedAtOffsetMinutes = offsetMinutes,
            BlobPath = originalRelativePath,
            DataSchemaVersion = 0,
            ProcessingStatus = ProcessingStatus.Pending,
            Payload = BuildPendingPayload(request, previewRelativePath)
        };

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<ITrackedEntryRepository>();

            await repository.AddAsync(newEntry).ConfigureAwait(false);
            await _backgroundAnalysisService.QueueEntryAsync(newEntry.EntryId, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Committed shared image {DraftId} as entry {EntryId} awaiting classification.", draftId, newEntry.EntryId);
        }
        finally
        {
            CleanupDraft(draftId, draft);
        }

        return newEntry;
    }

    public void Discard(Guid draftId)
    {
        var draft = _draftStore.Get(draftId);
        if (draft is null)
        {
            return;
        }

        CleanupDraft(draftId, draft);
    }

    private static string ResolveExtension(string? fileName, string? contentType)
    {
        var extension = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(extension))
        {
            return extension;
        }

        return contentType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/heic" => ".heic",
            "image/heif" => ".heif",
            "image/gif" => ".gif",
            _ => ".jpg"
        };
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };
    }

    private static (string? TimeZoneId, int? OffsetMinutes) ResolveTimeZoneMetadata(SharedImageMetadata metadata, DateTime capturedAtUtc)
    {
        var timeZoneId = metadata.CapturedAtTimeZoneId;
        var offsetMinutes = metadata.CapturedAtOffsetMinutes;

        if (timeZoneId is not null && offsetMinutes is not null)
        {
            return (timeZoneId, offsetMinutes);
        }

        if (offsetMinutes is int offset)
        {
            return (metadata.CapturedAtTimeZoneId, offset);
        }

        var capturedZone = DateTimeConverter.ResolveTimeZone(metadata.CapturedAtTimeZoneId, metadata.CapturedAtOffsetMinutes);
        if (capturedZone is not null)
        {
            var resolvedOffset = DateTimeConverter.GetUtcOffsetMinutes(capturedZone, capturedAtUtc);
            return (capturedZone.Id, resolvedOffset);
        }

        var (tzId, offsetMinutesFromNow) = DateTimeConverter.CaptureTimeZoneMetadata(capturedAtUtc);
        return (tzId, offsetMinutesFromNow);
    }

    private static PendingEntryPayload BuildPendingPayload(ShareEntryCommitRequest request, string previewPath)
    {
        return new PendingEntryPayload
        {
            Description = string.IsNullOrWhiteSpace(request.Description)
                ? "Shared photo"
                : request.Description,
            PreviewBlobPath = previewPath
        };
    }

    private void CleanupDraft(Guid draftId, SharedImageDraft draft)
    {
        try
        {
            if (File.Exists(draft.OriginalAbsolutePath))
            {
                File.Delete(draft.OriginalAbsolutePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete draft original file {Path}.", draft.OriginalAbsolutePath);
        }

        try
        {
            if (File.Exists(draft.PreviewAbsolutePath))
            {
                File.Delete(draft.PreviewAbsolutePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete draft preview file {Path}.", draft.PreviewAbsolutePath);
        }

        _draftStore.Remove(draftId);
    }
}
