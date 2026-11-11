using MetadataAnalyzer.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace MetadataAnalyzer.Core.Services;

public sealed class MetadataProviderOptions
{
    public bool UseMockData { get; set; } = true;
}

public sealed class MetadataProviderResolver(IServiceProvider services, MetadataProviderOptions options) : IMetadataProvider
{
    public Task<Models.AssemblyModel> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!options.UseMockData)
        {
            throw new NotSupportedException(
                "Only the mock metadata provider is available in stage 1. Set Application:UseMockData to true.");
        }

        return services.GetRequiredService<MockMetadataProvider>().LoadAsync(cancellationToken);
    }
}
