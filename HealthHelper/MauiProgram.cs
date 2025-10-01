using System.IO;
using CommunityToolkit.Maui;
using HealthHelper.Data;
using HealthHelper.Services.Analysis;
using HealthHelper.Services.Logging;
using HealthHelper.Services.Llm;
using Microsoft.Maui.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HealthHelper;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
            });

        var logsDirectory = Path.Combine(FileSystem.AppDataDirectory, "logs");
        var logFilePath = Path.Combine(logsDirectory, "healthhelper.log");
        const long maxLogFileSize = 1024 * 1024; // 1 MB

        builder.Logging.AddConsole();
        builder.Logging.AddProvider(new FileLoggerProvider(logFilePath, maxLogFileSize));

#if DEBUG
        builder.Logging.AddDebug();
#endif

        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "healthhelper.db3");
        builder.Services.AddDbContext<HealthHelperDbContext>(options => options.UseSqlite($"Filename={dbPath}"));

        builder.Services.AddScoped<ITrackedEntryRepository, SqliteTrackedEntryRepository>();
        builder.Services.AddScoped<IEntryAnalysisRepository, SqliteEntryAnalysisRepository>();
        builder.Services.AddScoped<IDailySummaryRepository, SqliteDailySummaryRepository>();

        builder.Services.AddSingleton<IAppSettingsRepository, SecureStorageAppSettingsRepository>();
        builder.Services.AddSingleton<ILogFileService>(_ => new LogFileService(logFilePath));


        builder.Services.AddTransient<MealLogViewModel>();
        builder.Services.AddTransient<MainPage>();

        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<SettingsPage>();

        builder.Services.AddTransient<MealDetailViewModel>();
        builder.Services.AddTransient<MealDetailPage>();

        builder.Services.AddTransient<IAnalysisOrchestrator, AnalysisOrchestrator>();
        builder.Services.AddTransient<ILLmClient, OpenAiLlmClient>();

        var app = builder.Build();

        // Run migrations
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<HealthHelperDbContext>();
            dbContext.Database.Migrate();
        }

        return app;
    }
}
