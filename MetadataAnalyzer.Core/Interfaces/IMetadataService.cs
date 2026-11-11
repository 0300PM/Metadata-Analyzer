using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Interfaces;

public interface IMetadataService
{
    Task<AssemblyModel> AnalyzeAsync(CancellationToken cancellationToken = default);

    Task<AssemblyModel> RefreshAsync(CancellationToken cancellationToken = default);

    AssemblyModel? Current { get; }
}
