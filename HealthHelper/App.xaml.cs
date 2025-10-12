using System;
using System.Threading.Tasks;
using HealthHelper.Services.Analysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HealthHelper;

public partial class App : Application
{
    private readonly ILogger<App> _logger;
    private readonly IServiceProvider _serviceProvider;

    public App(ILogger<App> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;

        InitializeComponent();

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // Recover any stale entries from previous app session
        // Use a scope since StaleEntryRecoveryService is scoped
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var recoveryService = scope.ServiceProvider.GetRequiredService<IStaleEntryRecoveryService>();
                await recoveryService.RecoverStaleEntriesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to run stale entry recovery on startup.");
            }
        });
    }

    public IServiceProvider Services => _serviceProvider;

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell());
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            _logger.LogError(exception, "Unhandled exception caught at domain level.");
        }
        else
        {
            _logger.LogError("Unhandled exception caught at domain level: {ExceptionObject}", e.ExceptionObject);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unobserved task exception.");
        e.SetObserved();
    }
}
