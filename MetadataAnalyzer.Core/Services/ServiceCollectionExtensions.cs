using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMetadataAnalyzerCore(
        this IServiceCollection services,
        bool useMockData = true,
        string? assemblyPath = null,
        ReflectionMetadataOptions? reflectionOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<IMetadataFormatter, MetadataFormatter>();
        services.AddSingleton<MockMetadataProvider>();
        services.AddSingleton(reflectionOptions ?? new ReflectionMetadataOptions());
        services.AddSingleton<ReflectionMetadataProvider>();
        services.AddSingleton(new MetadataProviderOptions { UseMockData = useMockData, FilePath = assemblyPath });
        services.AddSingleton<IMetadataProvider, MetadataProviderResolver>();
        services.AddSingleton<IMetadataRepository, InMemoryMetadataRepository>();
        services.AddSingleton<IMetadataService, MetadataService>();

        return services;
    }
}
