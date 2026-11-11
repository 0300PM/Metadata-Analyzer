namespace MetadataAnalyzer.Infrastructure.Logging;

public sealed class LoggingOptions
{
    public const string SectionName = "Logging";

    public bool Enabled { get; set; } = true;

    public string Provider { get; set; } = "File";

    public string FilePath { get; set; } = "Logs/application.log";

    public string MinimumLevel { get; set; } = "Information";

    public bool LogToConsole { get; set; } = true;
}
