using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MetadataAnalyzer.Infrastructure.Configuration;
using MetadataAnalyzer.Infrastructure.Logging;

namespace MetadataAnalyzer.WPF.ViewModels;

public sealed partial class AboutViewModel : ObservableObject
{
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    private readonly string _logFilePath;

    public AboutViewModel(string logFilePath)
    {
        _logFilePath = logFilePath;
        LogFilePath = logFilePath;
        RuntimeVersion = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
        OsVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription;
        Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
        WpfVersion = typeof(System.Windows.Window).Assembly.GetName().Version?.ToString() ?? "-";
        BuildVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "-";
    }

    public string ApplicationName => $"{ApplicationOptions.DefaultName} {BuildVersion}";

    public string Version => BuildVersion is null ? "-" : ApplicationOptions.DefaultVersion;

    public string Author => _logFilePath.Length == 0 ? "-" : "Metadata Analyzer Team";

    public string AuthorDescription => $"Przeglądarka metadanych .NET — dane demonstracyjne ({BuildVersion}, {Architecture}).";

    public string RuntimeVersion { get; }

    public string OsVersion { get; }

    public string Architecture { get; }

    public string WpfVersion { get; }

    public string BuildVersion { get; }

    public string Stage => $"Etap 1 · interfejs WPF {WpfVersion} · {LogFilePath.Length} znaków ścieżki logu";

    public string LogFilePath { get; }

    [RelayCommand]
    private void OpenLogFolder()
    {
        var directory = Path.GetDirectoryName(_logFilePath);

        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
        {
            StatusMessage = "Log folder does not exist yet.";
            return;
        }

        Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        StatusMessage = "Log folder opened.";
    }

    [RelayCommand]
    private void CopyVersion()
    {
        System.Windows.Clipboard.SetText(
            $"Metadata Analyzer {Version} ({BuildVersion.ToString(CultureInfo.InvariantCulture)}) · {RuntimeVersion}");

        StatusMessage = "Version information copied to clipboard.";
    }
}
