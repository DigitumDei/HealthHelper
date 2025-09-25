using HealthHelper.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HealthHelper.Data;

public class SecureStorageAppSettingsRepository : IAppSettingsRepository
{
    private const string AppSettingsKey = "app_settings";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<AppSettings> GetAppSettingsAsync()
    {
        var settingsJson = await SecureStorage.GetAsync(AppSettingsKey);
        if (string.IsNullOrEmpty(settingsJson))
        {
            return new AppSettings(); // Return default/empty settings
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(settingsJson, SerializerOptions);
            return settings ?? new AppSettings();
        }
        catch (JsonException)
        {
            // Data is corrupted or in an invalid format, return default settings
            return new AppSettings();
        }
    }

    public Task SaveAppSettingsAsync(AppSettings settings)
    {
        var settingsJson = JsonSerializer.Serialize(settings, SerializerOptions);
        return SecureStorage.SetAsync(AppSettingsKey, settingsJson);
    }
}
