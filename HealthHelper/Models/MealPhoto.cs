namespace HealthHelper.Models;

public sealed record MealPhoto(int EntryId, string FullPath, string Description, DateTimeOffset CapturedAt);
