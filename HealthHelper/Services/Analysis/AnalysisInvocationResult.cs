using HealthHelper.Models;

namespace HealthHelper.Services.Analysis;

public class AnalysisInvocationResult
{
    private AnalysisInvocationResult(bool isQueued, string? userMessage = null, bool requiresCredentials = false)
    {
        IsQueued = isQueued;
        UserMessage = userMessage;
        RequiresCredentials = requiresCredentials;
    }

    public bool IsQueued { get; }
    public string? UserMessage { get; }
    public bool RequiresCredentials { get; }

    public static AnalysisInvocationResult Success() => new(true);

    public static AnalysisInvocationResult MissingCredentials(LlmProvider provider) =>
        new(false, $"Add an API key for {provider} to enable analysis.", true);

    public static AnalysisInvocationResult MissingModel(LlmProvider provider) =>
        new(false, $"Select a model for {provider} before running analysis.");

    public static AnalysisInvocationResult NotSupported(LlmProvider provider) =>
        new(false, $"{provider} is not supported yet. Switch providers in Settings to analyze entries.");

    public static AnalysisInvocationResult NoAnalysis() =>
        new(false, "The analysis service did not return results. Try again later.");

    public static AnalysisInvocationResult Error() =>
        new(false, "Analysis failed. You can retry from the settings page.");
}
