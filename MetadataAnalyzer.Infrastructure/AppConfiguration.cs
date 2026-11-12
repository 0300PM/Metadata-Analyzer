using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Infrastructure.Configuration;
using MetadataAnalyzer.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MetadataAnalyzer.Infrastructure;

public static class AppConfiguration
{
    public const string ApplicationName = ApplicationOptions.DefaultName;

    public const string DefaultLogPath = "Logs/application.log";

    public static string ResolveLogFilePath(string? configuredPath)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath) ? DefaultLogPath : configuredPath;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }

    public static IServiceCollection AddMetadataAnalyzer(
        this IServiceCollection services,
        IConfiguration configuration,
        bool logToConsole,
        bool useInMemoryLog = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var application = configuration.GetSection(ApplicationOptions.SectionName).Get<ApplicationOptions>()
            ?? new ApplicationOptions();
        var logging = configuration.GetSection(LoggingOptions.SectionName).Get<LoggingOptions>()
            ?? new LoggingOptions();

        services.AddSingleton(configuration);
        services.AddSingleton(application);
        services.AddMetadataAnalyzerCore(application.UseMockData, application.AssemblyPath);
        services.AddMetadataAnalyzerLogging(options =>
        {
            options.Enabled = logging.Enabled;
            options.Provider = logging.Provider;
            options.FilePath = ResolveLogFilePath(logging.FilePath);
            options.MinimumLevel = SettingsService.NormalizeLevel(logging.MinimumLevel);
            options.LogToConsole = logToConsole;
        });

        if (useInMemoryLog)
        {
            services.AddSingleton<InMemoryLogSink>();
            services.AddSingleton<IAppLogger>(provider => new SettingsAwareLogger(
                provider.GetRequiredService<LoggingOptions>(),
                provider.GetRequiredService<InMemoryLogSink>(),
                provider.GetRequiredService<FileLogger>()));
        }

        return services;
    }
}
