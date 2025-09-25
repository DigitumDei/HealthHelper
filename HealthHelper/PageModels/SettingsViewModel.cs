using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HealthHelper.Data;
using HealthHelper.Models;
using System.Collections.ObjectModel;

namespace HealthHelper.PageModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly IAppSettingsRepository _appSettingsRepository;
    private AppSettings _appSettings;

    [ObservableProperty]
    private LlmProvider selectedProvider;

    [ObservableProperty]
    private string apiKey = string.Empty;

    [ObservableProperty]
    private bool isApiKeyMasked = true;

    public ObservableCollection<LlmProvider> Providers { get; }

    public string ToggleIconGlyph => IsApiKeyMasked ? FluentUI.eye_24_regular : FluentUI.eye_off_24_regular;

    public SettingsViewModel(IAppSettingsRepository appSettingsRepository)
    {
        _appSettingsRepository = appSettingsRepository;
        Providers = new ObservableCollection<LlmProvider>(Enum.GetValues<LlmProvider>());
        _appSettings = new AppSettings();
    }

    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        _appSettings = await _appSettingsRepository.GetAppSettingsAsync();
        SelectedProvider = _appSettings.SelectedProvider;
        if (_appSettings.ApiKeys.TryGetValue(SelectedProvider, out var key))
        {
            ApiKey = key;
        }
    }

    [RelayCommand]
    private async Task SaveSettings()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            await Shell.Current.DisplayAlertAsync("Error", "API Key cannot be empty.", "OK");
            return;
        }

        _appSettings.SelectedProvider = SelectedProvider;

        await _appSettingsRepository.SaveAppSettingsAsync(_appSettings);
        await Shell.Current.DisplayAlertAsync("Success", "Settings saved.", "OK");
    }

    [RelayCommand]
    private void ToggleApiKeyVisibility()
    {
        IsApiKeyMasked = !IsApiKeyMasked;
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
