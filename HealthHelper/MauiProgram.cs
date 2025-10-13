using System.IO;
using CommunityToolkit.Maui;
using HealthHelper.Data;
using HealthHelper.PageModels;
using HealthHelper.Pages;
using HealthHelper.Services.Analysis;
using HealthHelper.Services.Logging;
using HealthHelper.Services.Media;
using HealthHelper.Services.Navigation;
using HealthHelper.Services.Llm;
using HealthHelper.Services.Share;
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
        builder.Services.AddSingleton<IBackgroundAnalysisService, BackgroundAnalysisService>();
        builder.Services.AddScoped<IStaleEntryRecoveryService, StaleEntryRecoveryService>();
        builder.Services.AddSingleton<IPendingPhotoStore, FilePendingPhotoStore>();

#if ANDROID
        builder.Services.AddSingleton<IPhotoResizer, AndroidPhotoResizer>();
        builder.Services.AddSingleton<ICameraCaptureService, AndroidCameraCaptureService>();
        builder.Services.AddScoped<IShareIntentProcessor, ShareIntentProcessor>();
#else
        builder.Services.AddSingleton<IPhotoResizer, NoOpPhotoResizer>();
        builder.Services.AddSingleton<ICameraCaptureService, MediaPickerCameraCaptureService>();
#endif

        builder.Services.AddSingleton<ISharedImageDraftStore, InMemorySharedImageDraftStore>();
        builder.Services.AddSingleton<IShareNavigationService, ShareNavigationService>();
        builder.Services.AddScoped<ISharedImageImportService, SharedImageImportService>();

        builder.Services.AddSingleton<HistoricalNavigationContext>();
        builder.Services.AddSingleton<IHistoricalNavigationService, HistoricalNavigationService>();


        builder.Services.AddTransient<EntryLogViewModel>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddTransient<WeekViewPage>();
        builder.Services.AddTransient<MonthViewPage>();
        builder.Services.AddTransient<YearViewPage>();
        builder.Services.AddTransient<DayDetailPage>();

        builder.Services.AddTransient<SettingsViewModel>();
        builder.Services.AddTransient<SettingsPage>();

        builder.Services.AddTransient<MealDetailViewModel>();
        builder.Services.AddTransient<MealDetailPage>();
        builder.Services.AddTransient<SleepDetailViewModel>();
        builder.Services.AddTransient<SleepDetailPage>();

        builder.Services.AddTransient<DailySummaryViewModel>();
        builder.Services.AddTransient<DailySummaryPage>();
        builder.Services.AddTransient<ExerciseDetailViewModel>();
        builder.Services.AddTransient<ExerciseDetailPage>();
        builder.Services.AddTransient<ShareEntryViewModel>();
        builder.Services.AddTransient<ShareEntryPage>();

        builder.Services.AddTransient<IAnalysisOrchestrator, AnalysisOrchestrator>();
        builder.Services.AddTransient<IDailySummaryService, DailySummaryService>();
        builder.Services.AddTransient<ILLmClient, OpenAiLlmClient>();
        builder.Services.AddSingleton<MealAnalysisValidator>();

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
