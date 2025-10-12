#if !UNIT_TESTS
using Microsoft.Maui.Storage;
#endif
using System;
using System.Collections.Generic;
using System.IO;
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

        if (typeChanged)
        {
            try
            {
                RelocateEntryAssets(entry, detectedEntryType, logger);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to relocate assets for entry {EntryId} during classification update.", entry.EntryId);
            }
        }

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

    private static void RelocateEntryAssets(TrackedEntry entry, EntryType newType, ILogger logger)
    {
        if (newType is EntryType.Unknown or EntryType.DailySummary)
        {
            // Unknown entries remain in the staging directory; daily summaries do not manage blobs.
            return;
        }

        if (string.IsNullOrWhiteSpace(entry.BlobPath))
        {
            return;
        }

        var targetDirectory = Path.Combine("Entries", newType.ToStorageString());
        var pathUpdates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string? UpdatePathIfNeeded(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return relativePath;
            }

            if (pathUpdates.TryGetValue(relativePath, out var cached))
            {
                return cached;
            }

            var currentDirectory = Path.GetDirectoryName(relativePath);
            if (string.Equals(currentDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
            {
                pathUpdates[relativePath] = relativePath;
                return relativePath;
            }

            var movedPath = MoveAsset(relativePath, targetDirectory, logger);
            pathUpdates[relativePath] = movedPath ?? relativePath;
            return pathUpdates[relativePath];
        }

        entry.BlobPath = UpdatePathIfNeeded(entry.BlobPath);

        switch (entry.Payload)
        {
            case MealPayload mealPayload:
                mealPayload.PreviewBlobPath = UpdatePathIfNeeded(mealPayload.PreviewBlobPath);
                break;
            case ExercisePayload exercisePayload:
                exercisePayload.PreviewBlobPath = UpdatePathIfNeeded(exercisePayload.PreviewBlobPath);
                exercisePayload.ScreenshotBlobPath = UpdatePathIfNeeded(exercisePayload.ScreenshotBlobPath);
                break;
            case PendingEntryPayload pendingPayload:
                pendingPayload.PreviewBlobPath = UpdatePathIfNeeded(pendingPayload.PreviewBlobPath);
                break;
        }
    }

    private static string? MoveAsset(string relativePath, string targetDirectory, ILogger logger)
    {
        try
        {
            var baseDirectory = GetAppDataDirectory();
            var sourceAbsolute = Path.Combine(baseDirectory, relativePath);
            if (!File.Exists(sourceAbsolute))
            {
                logger.LogDebug("Asset {RelativePath} not found when relocating entry files.", relativePath);
                return null;
            }

            var fileName = Path.GetFileName(relativePath);
            var destinationRelative = Path.Combine(targetDirectory, fileName);
            var destinationAbsolute = Path.Combine(baseDirectory, destinationRelative);

            Directory.CreateDirectory(Path.Combine(baseDirectory, targetDirectory));

            if (string.Equals(sourceAbsolute, destinationAbsolute, StringComparison.OrdinalIgnoreCase))
            {
                return destinationRelative;
            }

            if (File.Exists(destinationAbsolute))
            {
                var uniqueName = $"{Path.GetFileNameWithoutExtension(fileName)}_{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
                destinationRelative = Path.Combine(targetDirectory, uniqueName);
                destinationAbsolute = Path.Combine(baseDirectory, destinationRelative);
            }

            File.Move(sourceAbsolute, destinationAbsolute);
            return destinationRelative;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to move asset {RelativePath} into {TargetDirectory}.", relativePath, targetDirectory);
            return null;
        }
    }

#if UNIT_TESTS
    private static string GetAppDataDirectory() => AppContext.BaseDirectory;
#else
    private static string GetAppDataDirectory() => FileSystem.AppDataDirectory;
#endif
}
