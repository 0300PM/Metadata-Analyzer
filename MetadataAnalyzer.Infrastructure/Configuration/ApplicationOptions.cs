namespace MetadataAnalyzer.Infrastructure.Configuration;

public sealed class ApplicationOptions
{
    public const string SectionName = "Application";

    public const string DefaultName = "Metadata Analyzer";

    public const string DefaultVersion = "1.0.0";

    public string Name { get; set; } = DefaultName;

    public string Version { get; set; } = DefaultVersion;

    public bool UseMockData { get; set; } = true;

    public string? AssemblyPath { get; set; }

    public bool ShowDetailsPanel { get; set; } = true;
}
