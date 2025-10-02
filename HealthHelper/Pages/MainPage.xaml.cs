using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.PageModels;
using HealthHelper.Services.Analysis;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;

namespace HealthHelper.Pages;

public partial class MainPage : ContentPage
{
    private readonly ITrackedEntryRepository _trackedEntryRepository;
    private readonly IBackgroundAnalysisService _backgroundAnalysisService;
    private readonly ILogger<MainPage> _logger;

    public MainPage(
        MealLogViewModel viewModel,
        ITrackedEntryRepository trackedEntryRepository,
        IBackgroundAnalysisService backgroundAnalysisService,
        ILogger<MainPage> logger)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _trackedEntryRepository = trackedEntryRepository;
        _backgroundAnalysisService = backgroundAnalysisService;
        _logger = logger;
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
            _logger.LogInformation("TakePhotoButton_Clicked: Starting photo capture");

            if (!await EnsureCameraPermissionsAsync())
            {
                _logger.LogWarning("TakePhotoButton_Clicked: Camera permissions denied");
                return;
            }

            if (!MediaPicker.Default.IsCaptureSupported)
            {
                _logger.LogWarning("TakePhotoButton_Clicked: Camera not supported");
                await DisplayAlertAsync("Not Supported", "Camera is not available on this device.", "OK");
                return;
            }

            _logger.LogInformation("TakePhotoButton_Clicked: Launching camera");

            // Try-catch specifically for MediaPicker which can cause process termination
            FileResult? photo = null;
            try
            {
                photo = await MediaPicker.Default.CapturePhotoAsync();
            }
            catch (Exception cameraEx)
            {
                // If we get here, app wasn't killed but camera failed
                _logger.LogError(cameraEx, "TakePhotoButton_Clicked: Camera capture failed with exception");
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlertAsync("Camera Error", "Failed to capture photo. Please try again.", "OK");
                });
                return;
            }

            if (photo is null)
            {
                _logger.LogInformation("TakePhotoButton_Clicked: User cancelled photo capture or app was restarted");
                return;
            }

            _logger.LogInformation("TakePhotoButton_Clicked: Photo captured successfully, saving to disk");
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

            _logger.LogInformation("TakePhotoButton_Clicked: Photo saved, creating database entry");
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
            _logger.LogInformation("TakePhotoButton_Clicked: Database entry created with ID {EntryId}", newEntry.EntryId);

            if (BindingContext is MealLogViewModel vm)
            {
                _logger.LogInformation("TakePhotoButton_Clicked: Adding entry to UI");
                await vm.AddPendingEntryAsync(newEntry);
            }

            _logger.LogInformation("TakePhotoButton_Clicked: Queueing background analysis");
            await _backgroundAnalysisService.QueueEntryAsync(newEntry.EntryId);
            _logger.LogInformation("TakePhotoButton_Clicked: Photo capture completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TakePhotoButton_Clicked: FATAL ERROR during photo capture");

            // Use MainThread to ensure UI call succeeds
            try
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlertAsync("Error", $"An error occurred: {ex.Message}", "OK");
                });
            }
            catch (Exception alertEx)
            {
                _logger.LogError(alertEx, "TakePhotoButton_Clicked: Failed to display error alert");
            }
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