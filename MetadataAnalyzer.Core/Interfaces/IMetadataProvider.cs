using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Interfaces;

public sealed record MetadataSource(string? FilePath = null)
{
    public bool IsFile => !string.IsNullOrWhiteSpace(FilePath);

    public static MetadataSource Empty { get; } = new();
}

public interface IMetadataProvider
{
    Task<AssemblyModel> LoadAsync(CancellationToken cancellationToken = default);

    Task<AssemblyModel> LoadAsync(MetadataSource source, CancellationToken cancellationToken = default);
}
