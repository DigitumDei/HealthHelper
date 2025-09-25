using System.Collections.Generic;

namespace HealthHelper.Models;

public class AppSettings
{
    public LlmProvider SelectedProvider { get; set; }
    public Dictionary<LlmProvider, string> ApiKeys { get; set; } = new();
}
