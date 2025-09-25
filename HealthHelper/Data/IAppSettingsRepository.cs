using HealthHelper.Models;

namespace HealthHelper.Data;

public interface IAppSettingsRepository
{
    Task<AppSettings> GetAppSettingsAsync();
    Task SaveAppSettingsAsync(AppSettings settings);
}
