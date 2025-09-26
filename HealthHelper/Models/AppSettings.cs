using System.Collections.Generic;

namespace HealthHelper.Models;

public class AppSettings
{
    public LlmProvider SelectedProvider { get; set; }
    public Dictionary<LlmProvider, string> ApiKeys { get; set; } = new();
    public Dictionary<LlmProvider, string> ModelPreferences { get; set; } = new();

    public string? GetModelPreference(LlmProvider provider)
    {
        return ModelPreferences.TryGetValue(provider, out var model) && !string.IsNullOrWhiteSpace(model)
            ? model
            : null;
    }
}
