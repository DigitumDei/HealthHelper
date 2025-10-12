namespace HealthHelper.Models;

/// <summary>
/// Temporary payload used while an entry is awaiting unified analysis classification.
/// </summary>
public class PendingEntryPayload : IEntryPayload
{
    public string? Description { get; set; }
    public string? PreviewBlobPath { get; set; }
}
