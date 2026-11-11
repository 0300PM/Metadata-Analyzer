namespace MetadataAnalyzer.Infrastructure.Configuration;

public sealed record AppSettings(
    string LogFilePath,
    string LogLevel,
    bool UseMockData,
    bool ShowDetailsPanel)
{
    public const string LogDirectory = "Logs";

    public const string DefaultLogFileName = "application.log";

    public static AppSettings Default { get; } = new(
        Path.Combine(LogDirectory, DefaultLogFileName),
        "Information",
        UseMockData: true,
        ShowDetailsPanel: true);
}
