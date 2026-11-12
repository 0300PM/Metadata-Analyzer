using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Interfaces;

public interface IMetadataService
{
    Task<AssemblyModel> AnalyzeAsync(CancellationToken cancellationToken = default);

    Task<AssemblyModel> AnalyzeAsync(MetadataSource source, CancellationToken cancellationToken = default);

    Task<AssemblyModel> RefreshAsync(CancellationToken cancellationToken = default);

    Task<AssemblyModel?> LoadAsync(string assemblyName, CancellationToken cancellationToken = default);

    Task SaveAsync(AssemblyModel assembly, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);

    AssemblyModel? Current { get; }
}
