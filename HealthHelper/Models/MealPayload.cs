
namespace HealthHelper.Models;

public class MealPayload : IEntryPayload
{
    public string? Description { get; set; }
    public string? ServingSize { get; set; }
}
