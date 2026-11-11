using System.Windows;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Services;
using MetadataAnalyzer.Infrastructure;
using MetadataAnalyzer.Infrastructure.Configuration;
using MetadataAnalyzer.Infrastructure.Logging;
using MetadataAnalyzer.WPF.ViewModels;
using MetadataAnalyzer.WPF.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MetadataAnalyzer.WPF;

public partial class App : Application
{
    private ServiceProvider? _services;

    internal static ServiceProvider? Services => ((App)Current)._services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            _services = BuildServiceProvider();

            var logger = _services.GetRequiredService<IAppLogger>();
            await logger.LogInformationAsync(LogEvents.ApplicationStarted);

            var window = _services.GetRequiredService<MainWindow>();
            MainWindow = window;
            AttachShell(window, _services.GetRequiredService<MainViewModel>());
            window.Show();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"The application could not start.{Environment.NewLine}{exception.Message}",
                "Metadata Analyzer",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_services is not null)
        {
            try
            {
                _services.GetRequiredService<IAppLogger>()
                    .LogInformationAsync(LogEvents.ApplicationClosed)
                    .GetAwaiter()
                    .GetResult();
            }
            catch (Exception)
            {
                // Logging must never block shutdown.
            }

            _services.Dispose();
        }

        base.OnExit(e);
    }

    private static ServiceProvider BuildServiceProvider()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        var settings = SettingsService.Load(configuration);

        var services = new ServiceCollection();

        services.AddMetadataAnalyzer(configuration, logToConsole: false, useInMemoryLog: true);
        services.AddSingleton(settings);
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}
