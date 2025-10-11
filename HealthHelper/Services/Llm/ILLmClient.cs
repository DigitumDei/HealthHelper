using HealthHelper.Models;
using System.Threading.Tasks;

namespace HealthHelper.Services.Llm;

public interface ILLmClient
{
    Task<LlmAnalysisResult> InvokeAnalysisAsync(
        TrackedEntry entry,
        LlmRequestContext context,
        string? existingAnalysisJson = null,
        string? correction = null);

    Task<LlmAnalysisResult> InvokeDailySummaryAsync(
        DailySummaryRequest summaryRequest,
        LlmRequestContext context,
        string? existingSummaryJson = null);
}

public class LlmRequestContext
{
    public string ModelId { get; set; } = string.Empty;
    public LlmProvider Provider { get; set; }
    public string ApiKey { get; set; } = string.Empty;
}

public class LlmAnalysisResult
{
    public EntryAnalysis? Analysis { get; set; }
    public LlmDiagnostics? Diagnostics { get; set; }
    public bool IsSuccess => Analysis != null;
}

public class LlmDiagnostics
{
    public int? PromptTokenCount { get; set; }
    public int? CompletionTokenCount { get; set; }
    public int? TotalTokenCount { get; set; }

    // Rate limit headers
    public string? RateLimitRequests { get; set; }
    public string? RateLimitRemainingRequests { get; set; }
    public string? RateLimitResetRequests { get; set; }
}
