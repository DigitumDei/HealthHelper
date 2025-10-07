using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace HealthHelper.Services.Media;

public sealed class FilePendingPhotoStore : IPendingPhotoStore
{
    private readonly string _storePath;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public FilePendingPhotoStore()
    {
        _storePath = Path.Combine(FileSystem.AppDataDirectory, "pending_capture.json");
    }

    public async Task SaveAsync(PendingPhotoCapture capture, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(capture);
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);

        await using var stream = File.Create(_storePath);
        await JsonSerializer.SerializeAsync(stream, capture, _serializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PendingPhotoCapture?> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_storePath))
        {
            return null;
        }

        await using var stream = File.OpenRead(_storePath);
        return await JsonSerializer.DeserializeAsync<PendingPhotoCapture>(stream, _serializerOptions, cancellationToken).ConfigureAwait(false);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_storePath))
        {
            File.Delete(_storePath);
        }

        return Task.CompletedTask;
    }
}
