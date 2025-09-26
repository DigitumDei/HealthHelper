using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Data;
using HealthHelper.Models;

namespace HealthHelper.PageModels;

[QueryProperty(nameof(Meal), "Meal")]
public partial class MealDetailViewModel : ObservableObject
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly IEntryAnalysisRepository _entryAnalysisRepository;

    [ObservableProperty]
    private MealPhoto meal;

    [ObservableProperty]
    private string analysisText;

    public MealDetailViewModel(ITrackedEntryRepository trackedEntryRepository, IEntryAnalysisRepository entryAnalysisRepository)
    {
        _trackedEntryRepository = trackedEntryRepository;
        _entryAnalysisRepository = entryAnalysisRepository;
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (Meal is null) return;

        await _trackedEntryRepository.DeleteAsync(Meal.EntryId);

        // Also delete the file
        if (File.Exists(Meal.FullPath))
        {
            File.Delete(Meal.FullPath);
        }

        await Shell.Current.GoToAsync("..");
    }

    partial void OnMealChanged(MealPhoto value)
    {
        LoadAnalysis();
    }

    private async void LoadAnalysis()
    {
        if (Meal is null) return;

        var analysis = await _entryAnalysisRepository.GetByTrackedEntryIdAsync(Meal.EntryId);
        if (analysis is not null)
        {
            AnalysisText = analysis.InsightsJson;
        }
        else
        {
            AnalysisText = "No analysis available for this entry.";
        }
    }
}
