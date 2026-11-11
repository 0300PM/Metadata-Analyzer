using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Interfaces;

public interface IMetadataProvider
{
    Task<AssemblyModel> LoadAsync(CancellationToken cancellationToken = default);
}
