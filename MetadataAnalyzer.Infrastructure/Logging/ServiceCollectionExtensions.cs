using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Infrastructure.Logging;

namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMetadataAnalyzerLogging(
        this IServiceCollection services,
        Action<LoggingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new LoggingOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton<IAppLogger>(_ => new FileLogger(options));
        services.AddSingleton<FileLogger>(_ => new FileLogger(options));

        return services;
    }
}
