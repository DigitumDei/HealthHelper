using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using HealthHelper.Models;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;

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
        var settingsJson = await InvokeOnMainThreadAsync(() => SecureStorage.Default.GetAsync(AppSettingsKey));
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
        return InvokeOnMainThreadAsync(() => SecureStorage.Default.SetAsync(AppSettingsKey, settingsJson));
    }

    private static Task<T> InvokeOnMainThreadAsync<T>(Func<Task<T>> action)
    {
        if (MainThread.IsMainThread)
        {
            return action();
        }

        var tcs = new TaskCompletionSource<T>();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                var result = await action().ConfigureAwait(false);
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    private static Task InvokeOnMainThreadAsync(Func<Task> action)
    {
        if (MainThread.IsMainThread)
        {
            return action();
        }

        var tcs = new TaskCompletionSource();

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                await action().ConfigureAwait(false);
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }
}
