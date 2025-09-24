
namespace HealthHelper.Models;

public class DailySummary
{
    public int SummaryId { get; set; }
    public Guid? ExternalId { get; set; }
    public DateTime SummaryDate { get; set; }
    public string Highlights { get; set; } = string.Empty;
    public string Recommendations { get; set; } = string.Empty;
}
