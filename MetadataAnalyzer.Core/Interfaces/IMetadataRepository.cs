using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Interfaces;

public interface IMetadataRepository
{
    string Name { get; }

    Task SaveAsync(AssemblyModel assembly, CancellationToken cancellationToken = default);

    Task<AssemblyModel?> LoadAsync(string assemblyName, CancellationToken cancellationToken = default);

    Task<AssemblyModel?> LoadLatestAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string assemblyName, CancellationToken cancellationToken = default);
}
