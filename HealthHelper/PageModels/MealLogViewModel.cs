using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Pages;

namespace HealthHelper.PageModels;

public partial class MealLogViewModel : ObservableObject
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    public ObservableCollection<MealPhoto> Meals { get; } = new();

    public MealLogViewModel(ITrackedEntryRepository trackedEntryRepository)
    {
        _trackedEntryRepository = trackedEntryRepository;
    }

    [RelayCommand]
    private async Task GoToMealDetail(MealPhoto meal)
    {
        if (meal is null) return;

        await Shell.Current.GoToAsync(nameof(MealDetailPage),
            new Dictionary<string, object>
            {
                { "Meal", meal }
            });
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
