using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HealthHelper.Services.Logging;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly string _logFilePath;
    private readonly long _maxFileSizeBytes;
    private readonly object _writeLock = new();
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public FileLoggerProvider(string logFilePath, long maxFileSizeBytes)
    {
        _logFilePath = logFilePath;
        _maxFileSizeBytes = maxFileSizeBytes;
        EnsureLogFileExists();
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FileLogger(name, this));
    }

    internal void WriteEntry(FileLogEntry entry)
    {
        var line = JsonSerializer.Serialize(entry, _serializerOptions);

        lock (_writeLock)
        {
            EnsureLogFileExists();
            RotateIfNeeded();
            File.AppendAllText(_logFilePath, line + Environment.NewLine);
        }
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

    private void RotateIfNeeded()
    {
        var fileInfo = new FileInfo(_logFilePath);
        if (!fileInfo.Exists || fileInfo.Length < _maxFileSizeBytes)
        {
            return;
        }

        var archivePath = _logFilePath + ".bak";
        File.Copy(_logFilePath, archivePath, overwrite: true);
        File.WriteAllText(_logFilePath, string.Empty);
    }

    public void Dispose()
    {
        _loggers.Clear();
    }

    private sealed class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly FileLoggerProvider _provider;

        public FileLogger(string categoryName, FileLoggerProvider provider)
        {
            _categoryName = categoryName;
            _provider = provider;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);

            var entry = new FileLogEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Level = logLevel.ToString(),
                Category = _categoryName,
                EventId = eventId.Id,
                Message = message,
                Exception = exception?.ToString()
            };

            _provider.WriteEntry(entry);
        }
    }
}

public sealed record FileLogEntry
{
    public DateTimeOffset Timestamp { get; init; }
    public string Level { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public int EventId { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Exception { get; init; }
}
