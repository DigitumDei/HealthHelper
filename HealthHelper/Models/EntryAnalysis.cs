
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
}
