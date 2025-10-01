using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Data;
using HealthHelper.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;

namespace HealthHelper.PageModels;

[QueryProperty(nameof(Meal), "Meal")]
public partial class MealDetailViewModel : ObservableObject
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly IEntryAnalysisRepository _entryAnalysisRepository;
    private readonly ILogger<MealDetailViewModel> _logger;

    [ObservableProperty]
    private MealPhoto meal;

    [ObservableProperty]
    private string analysisText;

    public MealDetailViewModel(
        ITrackedEntryRepository trackedEntryRepository,
        IEntryAnalysisRepository entryAnalysisRepository,
        ILogger<MealDetailViewModel> logger)
    {
        _trackedEntryRepository = trackedEntryRepository;
        _entryAnalysisRepository = entryAnalysisRepository;
        _logger = logger;
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (Meal is null)
        {
            _logger.LogWarning("Delete command invoked without a selected meal.");
            return;
        }

        try
        {
            _logger.LogInformation("Deleting meal entry {EntryId}.", Meal.EntryId);
            await _trackedEntryRepository.DeleteAsync(Meal.EntryId).ConfigureAwait(false);

            if (File.Exists(Meal.FullPath))
            {
                File.Delete(Meal.FullPath);
            }

            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.GoToAsync(".."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete meal entry {EntryId}.", Meal.EntryId);
            await MainThread.InvokeOnMainThreadAsync(() => Shell.Current.DisplayAlertAsync("Delete failed", "We couldn't delete this meal. Try again later.", "OK"));
        }
    }

    partial void OnMealChanged(MealPhoto value)
    {
        _ = LoadAnalysisAsync();
    }

    private async Task LoadAnalysisAsync()
    {
        if (Meal is null)
        {
            return;
        }
        try
        {
            _logger.LogDebug("Loading analysis for entry {EntryId}.", Meal.EntryId);
            var analysis = await _entryAnalysisRepository.GetByTrackedEntryIdAsync(Meal.EntryId).ConfigureAwait(false);
            if (analysis is not null)
            {
                AnalysisText = analysis.InsightsJson;
            }
            else
            {
                AnalysisText = "No analysis available for this entry.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load analysis for entry {EntryId}.", Meal.EntryId);
            AnalysisText = "We couldn't load the analysis for this meal.";
        }
    }
}
