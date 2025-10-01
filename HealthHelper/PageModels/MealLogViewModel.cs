using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Pages;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

namespace HealthHelper.PageModels;

public partial class MealLogViewModel : ObservableObject
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly ILogger<MealLogViewModel> _logger;
    public ObservableCollection<MealPhoto> Meals { get; } = new();

    public MealLogViewModel(ITrackedEntryRepository trackedEntryRepository, ILogger<MealLogViewModel> logger)
    {
        _trackedEntryRepository = trackedEntryRepository;
        _logger = logger;
    }

    [RelayCommand]
    private async Task GoToMealDetail(MealPhoto meal)
    {
        if (meal is null)
        {
            _logger.LogWarning("Attempted to navigate to meal details with a null meal reference.");
            return;
        }

        try
        {
            _logger.LogInformation("Navigating to meal detail for entry {EntryId}.", meal.EntryId);
            await Shell.Current.GoToAsync(nameof(MealDetailPage),
                new Dictionary<string, object>
                {
                    { "Meal", meal }
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to navigate to meal detail for entry {EntryId}.", meal.EntryId);
            await Shell.Current.DisplayAlertAsync("Navigation error", "Unable to open meal details right now.", "OK");
        }
    }

    public async Task LoadEntriesAsync()
    {
        try
        {
            _logger.LogDebug("Loading meal entries for {Date}.", DateTime.UtcNow.Date);
            var entries = await _trackedEntryRepository.GetByDayAsync(DateTime.UtcNow).ConfigureAwait(false);

            var mealPhotos = entries
                .Where(entry => entry.BlobPath is not null && entry.Payload is MealPayload)
                .OrderByDescending(entry => entry.CapturedAt)
                .Select(entry =>
                {
                    var mealPayload = (MealPayload)entry.Payload!;
                    var fullPath = Path.Combine(FileSystem.AppDataDirectory, entry.BlobPath!);
                    return new MealPhoto(entry.EntryId, fullPath, mealPayload.Description ?? string.Empty, entry.CapturedAt);
                })
                .ToList();

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                Meals.Clear();
                foreach (var mealPhoto in mealPhotos)
                {
                    Meals.Add(mealPhoto);
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load meal entries.");
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync("Error", "Unable to load meals. Try again later.", "OK"));
        }
    }
}
