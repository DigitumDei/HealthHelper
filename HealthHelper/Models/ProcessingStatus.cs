namespace HealthHelper.Models;

public enum ProcessingStatus
{
    Pending = 0,      // Entry created, analysis not started
    Processing = 1,   // Analysis in progress
    Completed = 2,    // Analysis finished successfully
    Failed = 3,       // Analysis failed (can be retried)
    Skipped = 4       // Analysis skipped (no API key, unsupported provider, etc.)
}
