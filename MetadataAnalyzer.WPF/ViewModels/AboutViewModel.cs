using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Infrastructure.Configuration;
using MetadataAnalyzer.Infrastructure.Logging;

namespace MetadataAnalyzer.WPF.ViewModels;

/// <summary>
/// Dane okna „About” / „Application Information”.
/// Tylko do odczytu; nie wykonuje operacji blokujących ani dostępu do schowka.
/// </summary>
public sealed partial class AboutViewModel : ObservableObject
{
    private readonly IAppLogger _logger;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public AboutViewModel(IAppLogger logger, AppSettings? settings = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var logPath = settings?.LogFilePath ?? AppSettings.Default.LogFilePath;
        LogFilePath = SettingsService.ResolveLogPath(logPath);

        RuntimeVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        OsVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        WpfVersion = typeof(System.Windows.Window).Assembly.GetName().Version?.ToString() ?? "-";
        BuildVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "-";
        Version = ApplicationOptions.DefaultVersion;
        Stage = $"Etap 1 · {Version}";
    }

    public string ApplicationName => $"{ApplicationOptions.DefaultName} {BuildVersion}";

    public string Version { get; }

    public string Author { get; } = "Metadata Analyzer";

    public string RuntimeVersion { get; }

    public string OsVersion { get; }

    public string WpfVersion { get; }

    public string BuildVersion { get; }

    public string Stage { get; }

    public string LogFilePath { get; }

    public string LogFileState => DescribeFile(LogFilePath);

    [RelayCommand]
    private async Task OpenLogFolderAsync()
    {
        var directory = Path.GetDirectoryName(LogFilePath);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            StatusMessage = "Katalog logu jeszcze nie istnieje.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
            StatusMessage = "Otwarto katalog logu.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Nie można otworzyć katalogu: {exception.Message}";
            await _logger.LogWarningAsync($"Open log folder failed: {exception.Message}").ConfigureAwait(true);
        }
    }

    [RelayCommand]
    private void CopyVersion()
    {
        try
        {
            System.Windows.Clipboard.SetText(
                $"Metadata Analyzer {Version} ({BuildVersion}) · {RuntimeVersion}");

            StatusMessage = "Skopiowano informacje o wersji.";
        }
        catch (Exception exception)
        {
            StatusMessage = $"Nie można skopiować: {exception.Message}";
        }
    }

    private static string DescribeFile(string path)
    {
        if (!File.Exists(path))
        {
            return "plik jeszcze nie istnieje";
        }

        var length = new FileInfo(path).Length;
        var modified = File.GetLastWriteTime(path).ToString("HH:mm:ss", CultureInfo.InvariantCulture);

        return $"{SettingsService.FormatSize(length)} · ostatnia zmiana {modified}";
    }
}
