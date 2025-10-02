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
    private readonly IBackgroundAnalysisService _backgroundAnalysisService;

    public MainPage(
        MealLogViewModel viewModel,
        ITrackedEntryRepository trackedEntryRepository,
        IBackgroundAnalysisService backgroundAnalysisService)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _trackedEntryRepository = trackedEntryRepository;
        _backgroundAnalysisService = backgroundAnalysisService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _backgroundAnalysisService.StatusChanged += OnEntryStatusChanged;
        if (BindingContext is MealLogViewModel vm)
        {
            await vm.LoadEntriesAsync();
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _backgroundAnalysisService.StatusChanged -= OnEntryStatusChanged;
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
                DataSchemaVersion = 1,
                ProcessingStatus = ProcessingStatus.Pending
            };

            await _trackedEntryRepository.AddAsync(newEntry);

            if (BindingContext is MealLogViewModel vm)
            {
                await vm.AddPendingEntryAsync(newEntry);
            }

            await _backgroundAnalysisService.QueueEntryAsync(newEntry.EntryId);
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

        if (selectedMeal.ProcessingStatus == ProcessingStatus.Failed || selectedMeal.ProcessingStatus == ProcessingStatus.Skipped)
        {
            await vm.RetryAnalysisCommand.ExecuteAsync(selectedMeal);
        }
        else if (selectedMeal.IsClickable)
        {
            await vm.GoToMealDetailCommand.ExecuteAsync(selectedMeal);
        }

        if (sender is CollectionView collectionView)
        {
            collectionView.SelectedItem = null;
        }
    }

    private async void OnEntryStatusChanged(object? sender, EntryStatusChangedEventArgs e)
    {
        if (BindingContext is MealLogViewModel vm)
        {
            await vm.UpdateEntryStatusAsync(e.EntryId, e.Status);
        }

        if (e.Status == ProcessingStatus.Skipped)
        {
            bool openSettings = await DisplayAlertAsync("Connect LLM", "An API key is required for analysis. Please add one in settings.", "Open Settings", "Dismiss");
            if (openSettings)
            {
                await Shell.Current.GoToAsync(nameof(SettingsPage));
            }
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