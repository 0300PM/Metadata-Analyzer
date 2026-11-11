using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Infrastructure.Configuration;
using MetadataAnalyzer.Infrastructure.Logging;

namespace MetadataAnalyzer.WPF.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAppLogger _logger;
    private readonly string _settingsFilePath = SettingsService.SettingsFilePath;

    [ObservableProperty]
    private string _logFilePath = AppSettings.Default.LogFilePath;

    [ObservableProperty]
    private string _logLevel = "Information";

    [ObservableProperty]
    private bool _useMockData = true;

    [ObservableProperty]
    private bool _showDetailsPanel = true;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasError;

    public SettingsViewModel(AppSettings settings, IAppLogger logger)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        LogFilePath = settings.LogFilePath;
        LogLevel = settings.LogLevel;
        UseMockData = settings.UseMockData;
        ShowDetailsPanel = settings.ShowDetailsPanel;
    }

    public IReadOnlyList<string> LogLevels => _logger is null ? [] : SettingsService.LogLevels;

    public string SettingsFilePath => _settingsFilePath;

    public string ResolvedLogPath => SettingsService.ResolveLogPath(LogFilePath);

    public string LogFileDescription => DescribeFile(ResolvedLogPath);

    public bool CanSave => !string.IsNullOrWhiteSpace(LogFilePath);

    public event EventHandler? Closed;

    public async Task<bool> SaveAsync()
    {
        if (!CanSave)
        {
            HasError = true;
            StatusMessage = "Log file path cannot be empty.";
            return false;
        }

        try
        {
            var directory = Path.GetDirectoryName(ResolvedLogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await SettingsService
                .SaveAsync(new AppSettings(LogFilePath, LogLevel, UseMockData, ShowDetailsPanel))
                .ConfigureAwait(true);

            HasError = false;
            StatusMessage = "Settings saved. Restart the application to apply them.";
            await _logger.LogInformationAsync($"Settings saved (log level: {LogLevel})").ConfigureAwait(true);
            return true;
        }
        catch (Exception exception)
        {
            HasError = true;
            StatusMessage = $"Could not save settings: {exception.Message}";
            await _logger.LogErrorAsync($"Settings save failed: {exception.Message}").ConfigureAwait(true);
            return false;
        }
    }

    [RelayCommand]
    private async Task Save()
    {
        await SaveAsync().ConfigureAwait(true);
    }

    [RelayCommand]
    private void Cancel() => Closed?.Invoke(this, EventArgs.Empty);

    partial void OnLogFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(ResolvedLogPath));
        OnPropertyChanged(nameof(LogFileDescription));
        OnPropertyChanged(nameof(CanSave));
    }

    private static string DescribeFile(string path)
    {
        if (!File.Exists(path))
        {
            return "file does not exist yet";
        }

        var length = new FileInfo(path).Length;
        var modified = File.GetLastWriteTime(path).ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        return $"{SettingsService.FormatSize(length)} · last write {modified}";
    }
}
