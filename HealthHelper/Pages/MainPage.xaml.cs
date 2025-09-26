using System.Linq;
using System.Threading.Tasks;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.PageModels;
using HealthHelper.Services.Analysis;

namespace HealthHelper.Pages;

public partial class MainPage : ContentPage
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly IAnalysisOrchestrator _analysisOrchestrator;

    public MainPage(
        MealLogViewModel viewModel,
        ITrackedEntryRepository trackedEntryRepository,
        IAnalysisOrchestrator analysisOrchestrator)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _trackedEntryRepository = trackedEntryRepository;
        _analysisOrchestrator = analysisOrchestrator;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is MealLogViewModel vm)
        {
            await vm.LoadEntriesAsync();
        }
    }

    private async void TakePhotoButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (!await EnsureCameraPermissionsAsync())
            {
                return;
            }

            if (!MediaPicker.Default.IsCaptureSupported)
            {
                await DisplayAlertAsync("Not Supported", "Camera is not available on this device.", "OK");
                return;
            }

            FileResult? photo = await MediaPicker.Default.CapturePhotoAsync();

            if (photo is null)
            {
                return;
            }

            string mealPhotosDir = Path.Combine(FileSystem.AppDataDirectory, "Entries", "Meal");
            Directory.CreateDirectory(mealPhotosDir);
            string uniqueFileName = $"{Guid.NewGuid()}.jpg";
            string persistentFilePath = Path.Combine(mealPhotosDir, uniqueFileName);

            await using (Stream sourceStream = await photo.OpenReadAsync())
            {
                await using (FileStream localFileStream = File.Create(persistentFilePath))
                {
                    await sourceStream.CopyToAsync(localFileStream);
                }
            }

            var newEntry = new Models.TrackedEntry
            {
                EntryType = "Meal",
                CapturedAt = DateTime.UtcNow,
                BlobPath = Path.Combine("Entries", "Meal", uniqueFileName),
                Payload = new Models.MealPayload { Description = "New meal photo" },
                DataSchemaVersion = 1
            };

            await _trackedEntryRepository.AddAsync(newEntry);

            var analysisResult = await Task.Run(() => _analysisOrchestrator.ProcessEntryAsync(newEntry));
            if (!analysisResult.IsQueued && !string.IsNullOrWhiteSpace(analysisResult.UserMessage))
            {
                if (analysisResult.RequiresCredentials)
                {
                    bool openSettings = await DisplayAlertAsync("Connect LLM", analysisResult.UserMessage, "Open Settings", "Dismiss");
                    if (openSettings)
                    {
                        await Shell.Current.GoToAsync(nameof(SettingsPage));
                    }
                }
                else
                {
                    await DisplayAlertAsync("Analysis", analysisResult.UserMessage, "OK");
                }
            }

            if (BindingContext is MealLogViewModel vm)
            {
                await vm.LoadEntriesAsync();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", $"An error occurred: {ex.Message}", "OK");
        }
    }

    private async void MealsCollection_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BindingContext is not MealLogViewModel vm)
        {
            return;
        }

        if (e.CurrentSelection.FirstOrDefault() is not MealPhoto selectedMeal)
        {
            return;
        }

        await vm.GoToMealDetailCommand.ExecuteAsync(selectedMeal);

        if (sender is CollectionView collectionView)
        {
            collectionView.SelectedItem = null;
        }
    }

    private async Task<bool> EnsureCameraPermissionsAsync()
    {
        PermissionStatus cameraStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (cameraStatus != PermissionStatus.Granted)
        {
            if (Permissions.ShouldShowRationale<Permissions.Camera>())
            {
                await DisplayAlertAsync("Permissions", "Camera access is required to take a photo.", "OK");
            }

            cameraStatus = await Permissions.RequestAsync<Permissions.Camera>();
        }

        if (cameraStatus != PermissionStatus.Granted)
        {
            return false;
        }

#if ANDROID
        PermissionStatus storageStatus;
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            storageStatus = await Permissions.CheckStatusAsync<Permissions.Media>();
            if (storageStatus != PermissionStatus.Granted)
            {
                storageStatus = await Permissions.RequestAsync<Permissions.Media>();
            }
        }
        else
        {
            storageStatus = await Permissions.CheckStatusAsync<Permissions.StorageWrite>();
            if (storageStatus != PermissionStatus.Granted)
            {
                storageStatus = await Permissions.RequestAsync<Permissions.StorageWrite>();
            }
        }

        if (storageStatus != PermissionStatus.Granted)
        {
            return false;
        }
#endif
        return true;
    }

}
