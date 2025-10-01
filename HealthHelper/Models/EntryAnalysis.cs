
namespace HealthHelper.Models;

public class EntryAnalysis
{
    public int AnalysisId { get; set; }
    public int EntryId { get; set; }
    public Guid? ExternalId { get; set; }
    public string ProviderId { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public DateTime CapturedAt { get; set; }
    public string InsightsJson { get; set; } = string.Empty;

    /// <summary>
    /// Version of the JSON schema used in InsightsJson.
    /// Enables schema evolution and backward compatibility.
    /// </summary>
    public string SchemaVersion { get; set; } = "1.0";
}
