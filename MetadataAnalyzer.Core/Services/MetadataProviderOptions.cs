namespace MetadataAnalyzer.Core.Services;

public sealed class MetadataProviderOptions
{
    public bool UseMockData { get; set; } = true;

    public string? FilePath { get; set; }
}
