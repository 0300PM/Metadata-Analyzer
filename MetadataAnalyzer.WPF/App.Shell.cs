using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MetadataAnalyzer.Infrastructure.Configuration;
using MetadataAnalyzer.Infrastructure.Logging;
using MetadataAnalyzer.WPF.ViewModels;
using MetadataAnalyzer.WPF.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MetadataAnalyzer.WPF;

public partial class App
{
    private MainViewModel? _mainViewModel;
    private Window? _settingsWindow;
    private Window? _aboutWindow;
    private Window? _logViewerWindow;

    private void AttachShell(Window window, MainViewModel viewModel)
    {
        _mainViewModel = viewModel;
        _mainViewModel.SettingsRequested += (_, _) => ShowSettingsWindow();
        _mainViewModel.AboutRequested += (_, _) => ShowAboutWindow();
        _mainViewModel.LogViewerRequested += (_, _) => ShowLogViewerWindow();

        window.Closed += (_, _) => CloseChildWindows();
    }

    private void ShowSettingsWindow()
    {
        if (_services is null)
        {
            return;
        }

        try
        {
            _settingsWindow = ReuseOrCreate(_settingsWindow, () =>
            {
                var viewModel = new SettingsViewModel(
                    _services.GetRequiredService<AppSettings>(),
                    _services.GetRequiredService<Core.Interfaces.IAppLogger>());

                return new SettingsWindow(viewModel) { Owner = MainWindow };
            });

            _settingsWindow.Show();
            _settingsWindow.Activate();
        }
        catch (Exception exception)
        {
            ReportWindowFailure("Settings", exception);
        }
    }

    private void ShowAboutWindow()
    {
        if (_services is null)
        {
            return;
        }

        try
        {
            _aboutWindow = ReuseOrCreate(_aboutWindow, () =>
            {
                var viewModel = new AboutViewModel(
                    _services.GetRequiredService<Core.Interfaces.IAppLogger>(),
                    _services.GetRequiredService<AppSettings>());

                return new AboutWindow(viewModel) { Owner = MainWindow };
            });

            _aboutWindow.Show();
            _aboutWindow.Activate();
        }
        catch (Exception exception)
        {
            ReportWindowFailure("About", exception);
        }
    }

    private void ShowLogViewerWindow()
    {
        if (_services is null)
        {
            return;
        }

        try
        {
            _logViewerWindow = ReuseOrCreate(_logViewerWindow, () =>
            {
                var viewModel = new LogViewerViewModel(_services.GetRequiredService<InMemoryLogSink>());

                return new LogViewerWindow(viewModel) { Owner = MainWindow };
            });

            _logViewerWindow.Show();
            _logViewerWindow.Activate();
        }
        catch (Exception exception)
        {
            ReportWindowFailure("Log Viewer", exception);
        }
    }

    /// <summary>
    /// Okno dialogowe nie może wywrócić całej aplikacji: nieobsłużony wyjątek
    /// w programie WPF kończy proces, dlatego każda próba otwarcia okna jest izolowana.
    /// </summary>
    private void ReportWindowFailure(string windowName, Exception exception)
    {
        _settingsWindow = null;
        _aboutWindow = null;
        _logViewerWindow = null;

        var message = $"Nie udało się otworzyć okna {windowName}.{Environment.NewLine}{exception.Message}";

        try
        {
            var logger = _services?.GetService<Core.Interfaces.IAppLogger>();

            if (logger is not null)
            {
                logger.LogErrorAsync($"{windowName} window failed: {exception}").GetAwaiter().GetResult();
            }
        }
        catch (Exception)
        {
            // Logowanie nie może przesłonić właściwego błędu.
        }

        _mainViewModel?.ReportError(message);

        MessageBox.Show(
            message,
            "Metadata Analyzer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private Window ReuseOrCreate(Window? existing, Func<Window> factory)
    {
        if (existing is null)
        {
            return Watch(factory());
        }

        // Zamknięte okno WPF nie nadaje się do ponownego pokazania
        // (InvalidOperationException przy Show). Takie okno jest
        // porzucane — tworzone jest nowe.
        if (IsWindowClosed(existing))
        {
            existing.Closed -= OnChildWindowClosed;
            return Watch(factory());
        }

        return existing;
    }

    private Window Watch(Window window)
    {
        window.Closed += OnChildWindowClosed;
        return window;
    }

    private static bool IsWindowClosed(Window window)
    {
        // Jedyny wiarygodny sposób: próba dotknięcia uchwytu okna.
        // Zamknięte okno zgłasza tu InvalidOperationException.
        try
        {
            _ = new WindowInteropHelper(window).Handle;
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private void OnChildWindowClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, _settingsWindow)) _settingsWindow = null;
        if (ReferenceEquals(sender, _aboutWindow)) _aboutWindow = null;
        if (ReferenceEquals(sender, _logViewerWindow)) _logViewerWindow = null;
    }

    private void CloseChildWindows()
    {
        _settingsWindow?.Close();
        _aboutWindow?.Close();
        _logViewerWindow?.Close();
    }
}
