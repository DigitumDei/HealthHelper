using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using MauiShare = Microsoft.Maui.ApplicationModel.DataTransfer.Share;

namespace HealthHelper.Services.Logging;

public interface ILogFileService
{
    string LogFilePath { get; }
    Task ShareAsync(CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class LogFileService : ILogFileService
{
    private readonly string _logFilePath;

    public LogFileService(string logFilePath)
    {
        _logFilePath = logFilePath;
        EnsureLogFileExists();
    }

    public string LogFilePath => _logFilePath;

    public async Task ShareAsync(CancellationToken cancellationToken = default)
    {
        EnsureLogFileExists();

        var fileInfo = new FileInfo(_logFilePath);
        if (!fileInfo.Exists || fileInfo.Length == 0)
        {
            await MauiShare.RequestAsync(new ShareTextRequest
            {
                Text = "No diagnostics log entries yet.",
                Title = "HealthHelper Diagnostics Log"
            });
            return;
        }

        await MauiShare.RequestAsync(new ShareFileRequest
        {
            Title = "HealthHelper Diagnostics Log",
            File = new ShareFile(_logFilePath)
        });
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        EnsureLogFileExists();
        File.WriteAllText(_logFilePath, string.Empty);
        return Task.CompletedTask;
    }

    private void EnsureLogFileExists()
    {
        var directory = Path.GetDirectoryName(_logFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (!File.Exists(_logFilePath))
        {
            File.WriteAllText(_logFilePath, string.Empty);
        }
    }
}
