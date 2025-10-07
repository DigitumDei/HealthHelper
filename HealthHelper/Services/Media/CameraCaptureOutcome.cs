namespace HealthHelper.Services.Media;

public sealed class CameraCaptureOutcome
{
    private CameraCaptureOutcome(CameraCaptureStatus status, string? errorMessage = null)
    {
        Status = status;
        ErrorMessage = errorMessage;
    }

    public CameraCaptureStatus Status { get; }

    public string? ErrorMessage { get; }

    public static CameraCaptureOutcome Success() => new(CameraCaptureStatus.Success);

    public static CameraCaptureOutcome Canceled() => new(CameraCaptureStatus.Canceled);

    public static CameraCaptureOutcome Failed(string errorMessage) => new(CameraCaptureStatus.Failed, errorMessage);
}
