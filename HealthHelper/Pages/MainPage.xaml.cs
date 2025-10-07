using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.PageModels;
using HealthHelper.Services.Analysis;
using HealthHelper.Services.Media;
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
    private readonly IPhotoResizer _photoResizer;
    private readonly ICameraCaptureService _cameraCaptureService;
    private readonly IPendingPhotoStore _pendingPhotoStore;
    private bool _isCapturing;
    private bool _isProcessingPending;

    public MainPage(
        MealLogViewModel viewModel,
        ITrackedEntryRepository trackedEntryRepository,
        IBackgroundAnalysisService backgroundAnalysisService,
        ILogger<MainPage> logger,
        IPhotoResizer photoResizer,
        ICameraCaptureService cameraCaptureService,
        IPendingPhotoStore pendingPhotoStore)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _trackedEntryRepository = trackedEntryRepository;
        _backgroundAnalysisService = backgroundAnalysisService;
        _logger = logger;
        _photoResizer = photoResizer;
        _cameraCaptureService = cameraCaptureService;
        _pendingPhotoStore = pendingPhotoStore;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _backgroundAnalysisService.StatusChanged += OnEntryStatusChanged;
        if (BindingContext is MealLogViewModel vm)
        {
            await vm.LoadEntriesAsync();
        }

        await ProcessPendingCaptureAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _backgroundAnalysisService.StatusChanged -= OnEntryStatusChanged;
    }

    private async void TakePhotoButton_Clicked(object sender, EventArgs e)
    {
        if (_isCapturing)
        {
            _logger.LogInformation("TakePhotoButton_Clicked: Capture already in progress.");
            return;
        }

        PendingPhotoCapture? capture = null;
        bool captureFinalized = false;

        try
        {
            _isCapturing = true;
            _logger.LogInformation("TakePhotoButton_Clicked: Starting photo capture");

            await ProcessPendingCaptureAsync();

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

            capture = CreatePendingCaptureMetadata();
            await _pendingPhotoStore.SaveAsync(capture);

            _logger.LogInformation("TakePhotoButton_Clicked: Launching camera");
            var outcome = await _cameraCaptureService.CaptureAsync(capture);

            if (outcome.Status != CameraCaptureStatus.Success)
            {
                _logger.LogInformation("TakePhotoButton_Clicked: Camera capture did not succeed (Status: {Status}).", outcome.Status);
                CleanupCaptureFiles(capture);
                await _pendingPhotoStore.ClearAsync();

                if (outcome.Status == CameraCaptureStatus.Failed && !string.IsNullOrWhiteSpace(outcome.ErrorMessage))
                {
                    await DisplayAlertAsync("Camera Error", outcome.ErrorMessage, "OK");
                }

                return;
            }

            _logger.LogInformation("TakePhotoButton_Clicked: Camera capture returned, finalising photo");
            await FinalizePhotoCaptureAsync(capture);
            captureFinalized = true;
            await _pendingPhotoStore.ClearAsync();
            _logger.LogInformation("TakePhotoButton_Clicked: Photo capture completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TakePhotoButton_Clicked: FATAL ERROR during photo capture");

            if (capture is not null && !captureFinalized)
            {
                CleanupCaptureFiles(capture);
                await _pendingPhotoStore.ClearAsync();
            }

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
        finally
        {
            _isCapturing = false;
        }
    }

    private async Task ProcessPendingCaptureAsync()
    {
        if (_isProcessingPending)
        {
            _logger.LogDebug("ProcessPendingCaptureAsync: Already running. Skipping duplicate invocation.");
            return;
        }

        _isProcessingPending = true;

        PendingPhotoCapture? pending = null;
        bool finalized = false;

        try
        {
            pending = await _pendingPhotoStore.GetAsync();
            if (pending is null)
            {
                return;
            }

            if (!File.Exists(pending.OriginalAbsolutePath))
            {
                _logger.LogWarning("Pending photo capture missing original file at {Path}. Clearing pending state.", pending.OriginalAbsolutePath);
                CleanupCaptureFiles(pending);
                await _pendingPhotoStore.ClearAsync();
                return;
            }

            _logger.LogInformation("Processing pending photo captured at {CapturedAtUtc}.", pending.CapturedAtUtc);
            await FinalizePhotoCaptureAsync(pending);
            finalized = true;
            await _pendingPhotoStore.ClearAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process pending photo capture.");

            if (pending is not null && !finalized)
            {
                CleanupCaptureFiles(pending);
                await _pendingPhotoStore.ClearAsync();
            }

            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlertAsync("Photo Error", "We captured a photo but couldn't import it. Please try again.", "OK");
            });
        }
        finally
        {
            _isProcessingPending = false;
        }
    }

    private PendingPhotoCapture CreatePendingCaptureMetadata()
    {
        string relativeDirectory = Path.Combine("Entries", "Meal");
        string mealPhotosDir = Path.Combine(FileSystem.AppDataDirectory, relativeDirectory);
        Directory.CreateDirectory(mealPhotosDir);

        string originalFileName = $"{Guid.NewGuid()}.jpg";
        string previewFileName = $"{Path.GetFileNameWithoutExtension(originalFileName)}_preview.jpg";

        return new PendingPhotoCapture
        {
            OriginalRelativePath = Path.Combine(relativeDirectory, originalFileName),
            PreviewRelativePath = Path.Combine(relativeDirectory, previewFileName),
            CapturedAtUtc = DateTime.UtcNow
        };
    }

    private async Task FinalizePhotoCaptureAsync(PendingPhotoCapture capture)
    {
        string originalPath = capture.OriginalAbsolutePath;
        string previewPath = capture.PreviewAbsolutePath;

        if (!File.Exists(originalPath))
        {
            throw new FileNotFoundException("Captured photo file is missing.", originalPath);
        }

        if (new FileInfo(originalPath).Length == 0)
        {
            throw new InvalidOperationException("Captured photo file is empty.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(previewPath)!);

        File.Copy(originalPath, previewPath, overwrite: true);
        _logger.LogInformation("FinalizePhotoCaptureAsync: Preview copy refreshed at {PreviewPath}", previewPath);

        await _photoResizer.ResizeAsync(previewPath, 1280, 1280);
        _logger.LogInformation("FinalizePhotoCaptureAsync: Preview resized");

        var newEntry = new TrackedEntry
        {
            EntryType = "Meal",
            CapturedAt = capture.CapturedAtUtc,
            BlobPath = capture.OriginalRelativePath,
            Payload = new MealPayload
            {
                Description = "New meal photo",
                PreviewBlobPath = capture.PreviewRelativePath
            },
            DataSchemaVersion = 1,
            ProcessingStatus = ProcessingStatus.Pending
        };

        await _trackedEntryRepository.AddAsync(newEntry);
        _logger.LogInformation("FinalizePhotoCaptureAsync: Database entry created with ID {EntryId}", newEntry.EntryId);

        if (BindingContext is MealLogViewModel vm)
        {
            try
            {
                await vm.AddPendingEntryAsync(newEntry);
            }
            catch (Exception uiEx)
            {
                _logger.LogError(uiEx, "FinalizePhotoCaptureAsync: Failed to add entry {EntryId} to UI collection.", newEntry.EntryId);
            }
        }

        try
        {
            await _backgroundAnalysisService.QueueEntryAsync(newEntry.EntryId);
            _logger.LogInformation("FinalizePhotoCaptureAsync: Entry queued for background analysis");
        }
        catch (Exception queueEx)
        {
            _logger.LogError(queueEx, "FinalizePhotoCaptureAsync: Failed to queue background analysis for entry {EntryId}.", newEntry.EntryId);
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await DisplayAlertAsync("Analysis Delayed", "We saved your photo but couldn't start the analysis yet. Please retry from the meal card.", "OK");
            });
        }
    }

    private static void CleanupCaptureFiles(PendingPhotoCapture capture)
    {
        TryDelete(capture.OriginalAbsolutePath);
        TryDelete(capture.PreviewAbsolutePath);

        static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // ignore clean-up failures
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
