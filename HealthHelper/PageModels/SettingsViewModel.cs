using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Data;
using HealthHelper.Models;
using HealthHelper.Services.Logging;
using Microsoft.Extensions.Logging;

namespace HealthHelper.PageModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsRepository _appSettingsRepository;
    private readonly ILogFileService _logFileService;
    private readonly ILogger<SettingsViewModel> _logger;
    private AppSettings _appSettings;

    [ObservableProperty]
    private LlmProvider selectedProvider;

    [ObservableProperty]
    private string apiKey = string.Empty;

    [ObservableProperty]
    private bool isApiKeyMasked = true;

    public ObservableCollection<LlmProvider> Providers { get; }

    public string ToggleIconGlyph => IsApiKeyMasked ? FluentUI.eye_24_regular : FluentUI.eye_off_24_regular;

    public SettingsViewModel(
        IAppSettingsRepository appSettingsRepository,
        ILogFileService logFileService,
        ILogger<SettingsViewModel> logger)
    {
        _appSettingsRepository = appSettingsRepository;
        _logFileService = logFileService;
        _logger = logger;
        Providers = new ObservableCollection<LlmProvider>(Enum.GetValues<LlmProvider>());
        _appSettings = new AppSettings();
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        try
        {
            _appSettings = await _appSettingsRepository.GetAppSettingsAsync();
            SelectedProvider = _appSettings.SelectedProvider;
            if (_appSettings.ApiKeys.TryGetValue(SelectedProvider, out var key))
            {
                ApiKey = key;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load app settings.");
            await Shell.Current.DisplayAlertAsync("Error", "Unable to load settings.", "OK");
        }
    }

    [RelayCommand]
    private async Task SaveSettings()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ApiKey))
            {
                await Shell.Current.DisplayAlertAsync("Error", "API Key cannot be empty.", "OK");
                return;
            }

            _appSettings.SelectedProvider = SelectedProvider;

            await _appSettingsRepository.SaveAppSettingsAsync(_appSettings);
            _logger.LogInformation("Settings saved for provider {Provider}.", SelectedProvider);
            await Shell.Current.DisplayAlertAsync("Success", "Settings saved.", "OK");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save app settings.");
            await Shell.Current.DisplayAlertAsync("Error", "Unable to save settings.", "OK");
        }
    }

    [RelayCommand]
    private void ToggleApiKeyVisibility()
    {
        IsApiKeyMasked = !IsApiKeyMasked;
    }

    [RelayCommand]
    private async Task ShareDiagnosticsLogAsync()
    {
        try
        {
            await _logFileService.ShareAsync();
            _logger.LogInformation("Diagnostics log share initiated.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to share diagnostics log.");
            await Shell.Current.DisplayAlertAsync("Error", "Unable to share diagnostics log.", "OK");
        }
    }

    partial void OnSelectedProviderChanged(LlmProvider value)
    {
        IsApiKeyMasked = true;

        if (_appSettings.ApiKeys.TryGetValue(value, out var key))
        {
            ApiKey = key;
        }
        else
        {
            ApiKey = string.Empty;
        }
    }

    partial void OnApiKeyChanged(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            _appSettings.ApiKeys.Remove(SelectedProvider);
        }
        else
        {
            _appSettings.ApiKeys[SelectedProvider] = value;
        }
    }

    partial void OnIsApiKeyMaskedChanged(bool value)
    {
        OnPropertyChanged(nameof(ToggleIconGlyph));
    }
}
