using System.Collections.ObjectModel;
using HealthHelper.Data;
using HealthHelper.Models;

namespace HealthHelper.PageModels;

public class MealLogViewModel
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    public ObservableCollection<MealPhoto> Meals { get; } = new();

    public MealLogViewModel(ITrackedEntryRepository trackedEntryRepository)
    {
        _trackedEntryRepository = trackedEntryRepository;
    }

    public async Task LoadEntriesAsync()
    {
        var entries = await _trackedEntryRepository.GetByDayAsync(DateTime.UtcNow);
        Meals.Clear();
        foreach (var entry in entries.OrderByDescending(e => e.CapturedAt))
        {
            if (entry.BlobPath is not null && entry.Payload is MealPayload mealPayload)
            {
                var fullPath = Path.Combine(FileSystem.AppDataDirectory, entry.BlobPath);
                Meals.Add(new MealPhoto(entry.EntryId, fullPath, mealPayload.Description ?? "", entry.CapturedAt));
            }
        }
    }
}
