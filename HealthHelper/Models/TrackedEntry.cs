
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthHelper.Models;

public class TrackedEntry
{
    public int EntryId { get; set; }
    public Guid? ExternalId { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
    public string? BlobPath { get; set; }
    public string DataPayload { get; set; } = string.Empty;
    public int DataSchemaVersion { get; set; }
    public ProcessingStatus ProcessingStatus { get; set; } = ProcessingStatus.Pending;

    [NotMapped]
    public IEntryPayload Payload { get; set; } = new MealPayload();
}
