using System.Globalization;
using System.Text.Json;
using MetadataAnalyzer.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;

namespace MetadataAnalyzer.Infrastructure.Configuration;

public static class SettingsService
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    private static readonly string[] EnabledLevels = ["Debug", "Information", "Warning", "Error"];

    public static IReadOnlyList<string> LogLevels => EnabledLevels;

    public static string SettingsFilePath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "appsettings.json"));

    public static string ResolveLogPath(string? configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath) ? AppSettings.Default.LogFilePath : configuredPath;
        return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    public static AppSettings Load(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var application = configuration.GetSection(ApplicationOptions.SectionName).Get<ApplicationOptions>()
            ?? new ApplicationOptions();
        var logging = configuration.GetSection(LoggingOptions.SectionName).Get<LoggingOptions>()
            ?? new LoggingOptions();

        return new AppSettings(
            ResolveLogPath(logging.FilePath),
            NormalizeLevel(logging.MinimumLevel),
            application.UseMockData,
            application.ShowDetailsPanel);
    }

    public static async Task SaveAsync(AppSettings settings, ApplicationOptions? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var payload = new Dictionary<string, object?>
        {
            [ApplicationOptions.SectionName] = new Dictionary<string, object?>
            {
                ["Name"] = ApplicationOptions.DefaultName,
                ["Version"] = ApplicationOptions.DefaultVersion,
                ["UseMockData"] = settings.UseMockData,
                ["AssemblyPath"] = configuration?.AssemblyPath,
                ["RepositoryPath"] = string.IsNullOrWhiteSpace(configuration?.RepositoryPath)
                    ? "Repository"
                    : configuration.RepositoryPath,
                ["ShowDetailsPanel"] = settings.ShowDetailsPanel,
            },
            [LoggingOptions.SectionName] = new Dictionary<string, object?>
            {
                ["Enabled"] = true,
                ["Provider"] = "File",
                ["FilePath"] = settings.LogFilePath,
                ["MinimumLevel"] = settings.LogLevel,
            },
        };

        var json = JsonSerializer.Serialize(payload, WriteOptions);
        await File.WriteAllTextAsync(SettingsFilePath, json).ConfigureAwait(false);
    }

    public static string NormalizeLevel(string? level) => level switch
    {
        "Debug" => "Debug",
        "Warning" => "Warning",
        "Error" => "Error",
        "Information" => "Information",
        _ => EnabledLevels.Where(candidate => string.Equals(candidate, level, StringComparison.OrdinalIgnoreCase))
            .Select(candidate => candidate)
            .FirstOrDefault() ?? "Information",
    };

    public static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => bytes.ToString(CultureInfo.InvariantCulture) + " B",
        < 1024 * 1024 => (bytes / 1024d).ToString("0.0", CultureInfo.InvariantCulture) + " KB",
        _ => (bytes / (1024d * 1024d)).ToString("0.0", CultureInfo.InvariantCulture) + " MB",
    };
}
