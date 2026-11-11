using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMetadataAnalyzerCore(
        this IServiceCollection services,
        bool useMockData = true)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IMetadataFormatter, MetadataFormatter>();
        services.AddSingleton<MockMetadataProvider>();
        services.AddSingleton(new MetadataProviderOptions { UseMockData = useMockData });
        services.AddSingleton<IMetadataProvider, MetadataProviderResolver>();
        services.AddSingleton<IMetadataService, MetadataService>();

        return services;
    }
}
