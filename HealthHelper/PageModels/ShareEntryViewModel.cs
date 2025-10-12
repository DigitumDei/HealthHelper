using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Services.Share;
using HealthHelper.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls;

namespace HealthHelper.PageModels;

[QueryProperty(nameof(DraftId), "DraftId")]
public partial class ShareEntryViewModel : ObservableObject
{
    private readonly ISharedImageDraftStore _draftStore;
    private readonly ISharedImageImportService _sharedImageImportService;
    private readonly ILogger<ShareEntryViewModel> _logger;

    private SharedImageDraft? _draft;

    public ShareEntryViewModel(
        ISharedImageDraftStore draftStore,
        ISharedImageImportService sharedImageImportService,
        ILogger<ShareEntryViewModel> logger)
    {
        _draftStore = draftStore;
        _sharedImageImportService = sharedImageImportService;
        _logger = logger;
    }

    [ObservableProperty]
    private Guid draftId;

    [ObservableProperty]
    private string previewPath = string.Empty;

    [ObservableProperty]
    private string captureInfo = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private bool isBusy;

    partial void OnDraftIdChanged(Guid value)
    {
        _ = LoadDraftAsync(value);
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (_draft is null)
        {
            _logger.LogWarning("Confirm invoked without an active draft.");
            return;
        }

        try
        {
            IsBusy = true;

            var request = new ShareEntryCommitRequest
            {
                EntryType = string.Empty,
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim()
            };

            await _sharedImageImportService.CommitAsync(DraftId, request).ConfigureAwait(false);

            await Shell.Current.GoToAsync("..", true).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to commit shared draft {DraftId}.", DraftId);
            await Shell.Current.DisplayAlertAsync("Save Failed", "We couldn't save this shared image. Try again.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        _sharedImageImportService.Discard(DraftId);
        await Shell.Current.GoToAsync("..", true);
    }

    private async Task LoadDraftAsync(Guid draftId)
    {
        try
        {
            _draft = _draftStore.Get(draftId);
            if (_draft is null)
            {
                _logger.LogWarning("Draft {DraftId} not found when loading share page.", draftId);
                await Shell.Current.DisplayAlertAsync("Missing Draft", "We couldn't load the shared image. Please try sharing again.", "OK");
                await Shell.Current.GoToAsync("..", true);
                return;
            }

            PreviewPath = _draft.PreviewAbsolutePath;

            var localCapturedAt = DateTimeConverter.ToOriginalLocal(
                _draft.Metadata.CapturedAtUtc,
                _draft.Metadata.CapturedAtTimeZoneId,
                _draft.Metadata.CapturedAtOffsetMinutes);

            CaptureInfo = $"Captured {localCapturedAt:MMM d, h:mm tt}";

            Description = string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load draft {DraftId}.", draftId);
            await Shell.Current.DisplayAlertAsync("Error", "We couldn't open the shared image. Please try again.", "OK");
            await Shell.Current.GoToAsync("..", true);
        }
    }
}
