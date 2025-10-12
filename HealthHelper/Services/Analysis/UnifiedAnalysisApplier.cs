using System;
using HealthHelper.Data;
using HealthHelper.Models;
using Microsoft.Extensions.Logging;

namespace HealthHelper.Services.Analysis;

/// <summary>
/// Applies unified analysis results to tracked entries, converting payloads and persisting updates.
/// </summary>
internal static class UnifiedAnalysisApplier
{
    public static async Task ApplyAsync(
        TrackedEntry entry,
        EntryType detectedEntryType,
        ITrackedEntryRepository repository,
        ILogger logger)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        if (repository is null)
        {
            throw new ArgumentNullException(nameof(repository));
        }

        if (logger is null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        var originalType = entry.EntryType;
        var pendingPayload = entry.Payload as PendingEntryPayload;
        var payloadConverted = false;

        if (pendingPayload is not null)
        {
            var converted = ConvertPendingPayload(entry, pendingPayload, detectedEntryType);
            if (!ReferenceEquals(converted, pendingPayload))
            {
                entry.Payload = converted;
                entry.DataSchemaVersion = 1;
                payloadConverted = true;
            }
            else
            {
                entry.Payload = converted;
            }
        }

        var typeChanged = originalType != detectedEntryType;
        if (!typeChanged && !payloadConverted)
        {
            return;
        }

        entry.EntryType = detectedEntryType;

        await repository.UpdateAsync(entry).ConfigureAwait(false);

        logger.LogInformation(
            "Updated entry {EntryId} classification to {EntryType} (payload={PayloadType}, schemaVersion={SchemaVersion}).",
            entry.EntryId,
            entry.EntryType.ToStorageString(),
            entry.Payload.GetType().Name,
            entry.DataSchemaVersion);
    }

    internal static IEntryPayload ConvertPendingPayload(TrackedEntry entry, PendingEntryPayload pendingPayload, EntryType detectedEntryType)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        if (pendingPayload is null)
        {
            throw new ArgumentNullException(nameof(pendingPayload));
        }

        return detectedEntryType switch
        {
            EntryType.Meal => new MealPayload
            {
                Description = pendingPayload.Description,
                PreviewBlobPath = pendingPayload.PreviewBlobPath ?? entry.BlobPath
            },
            EntryType.Exercise => new ExercisePayload
            {
                Description = pendingPayload.Description,
                PreviewBlobPath = pendingPayload.PreviewBlobPath ?? entry.BlobPath,
                ScreenshotBlobPath = entry.BlobPath ?? pendingPayload.PreviewBlobPath
            },
            _ => pendingPayload
        };
    }
}
