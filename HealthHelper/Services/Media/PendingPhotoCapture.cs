using System;
using System.IO;
using System.Text.Json.Serialization;
using Microsoft.Maui.Storage;

namespace HealthHelper.Services.Media;

public sealed class PendingPhotoCapture
{
    public string OriginalRelativePath { get; set; } = string.Empty;
    public string PreviewRelativePath { get; set; } = string.Empty;
    public DateTime CapturedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public string OriginalAbsolutePath => Path.Combine(FileSystem.AppDataDirectory, OriginalRelativePath);

    [JsonIgnore]
    public string PreviewAbsolutePath => Path.Combine(FileSystem.AppDataDirectory, PreviewRelativePath);
}
