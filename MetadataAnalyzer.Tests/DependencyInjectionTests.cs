using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Services;
using MetadataAnalyzer.Infrastructure;
using MetadataAnalyzer.Infrastructure.Configuration;
using MetadataAnalyzer.Infrastructure.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MetadataAnalyzer.Tests;

public sealed class DependencyInjectionTests
{
    private static ServiceProvider BuildProvider()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Application:UseMockData"] = "true",
                ["Logging:FilePath"] = "Logs/test.log",
                ["Logging:MinimumLevel"] = "Information",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddMetadataAnalyzer(configuration, logToConsole: false, useInMemoryLog: true);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
    }

    [Fact]
    public void ContainerValidatesOnBuild()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider);
    }

    [Fact]
    public void CoreServicesAreRegisteredAsSingletons()
    {
        using var provider = BuildProvider();

        Assert.Same(provider.GetRequiredService<IMetadataService>(), provider.GetRequiredService<IMetadataService>());
        Assert.Same(provider.GetRequiredService<IMetadataFormatter>(), provider.GetRequiredService<IMetadataFormatter>());
        Assert.Same(provider.GetRequiredService<ISystemClock>(), provider.GetRequiredService<ISystemClock>());
        Assert.Same(provider.GetRequiredService<IMetadataProvider>(), provider.GetRequiredService<IMetadataProvider>());
    }

    [Fact]
    public void LoggerIsSettingsAwareAndSharedWithSink()
    {
        using var provider = BuildProvider();

        var logger = provider.GetRequiredService<IAppLogger>();
        var sink = provider.GetRequiredService<InMemoryLogSink>();

        Assert.IsType<SettingsAwareLogger>(logger);
        Assert.Empty(sink.Entries);
    }

    [Fact]
    public async Task LoggingWritesToBothFileAndSink()
    {
        using var provider = BuildProvider();
        var logger = provider.GetRequiredService<IAppLogger>();
        var sink = provider.GetRequiredService<InMemoryLogSink>();

        await logger.LogInformationAsync("di probe");

        Assert.Contains(sink.Entries, entry => entry.Message == "di probe");
        // Dwie rejestracje IAppLogger: FileLogger (rejestrowany w AddMetadataAnalyzerLogging
        // jako konkret) oraz SettingsAwareLogger — ostatnia wygrywa przy GetRequiredService.
        Assert.Equal(2, provider.GetServices<IAppLogger>().Count());
    }

    [Fact]
    public async Task ProviderIsResolvedThroughResolverHonouringMockFlag()
    {
        using var provider = BuildProvider();

        var resolver = provider.GetRequiredService<IMetadataProvider>();

        Assert.IsType<MetadataProviderResolver>(resolver);
        Assert.Equal("SampleLibrary.dll", (await resolver.LoadAsync()).FileName);
    }

    [Fact]
    public async Task ResolverRequiresPathForReflectionMode()
    {
        using var provider = BuildProvider();
        var resolver = new MetadataProviderResolver(
            provider,
            new MetadataProviderOptions { UseMockData = false });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.LoadAsync());

        Assert.Contains("assembly file path", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RepositoryIsRegisteredAndRoundTripsAssembly()
    {
        using var provider = BuildProvider();
        var repository = provider.GetRequiredService<IMetadataRepository>();
        var assembly = await provider.GetRequiredService<IMetadataProvider>().LoadAsync();

        await repository.SaveAsync(assembly);

        Assert.Same(repository, provider.GetRequiredService<IMetadataRepository>());
        Assert.Same(assembly, await repository.LoadAsync(assembly.Name));
        Assert.Same(assembly, await repository.LoadLatestAsync());
        Assert.Equal([assembly.Name], await repository.ListAsync());
        Assert.True(await repository.DeleteAsync(assembly.Name));
        Assert.False(await repository.DeleteAsync(assembly.Name));
        Assert.Empty(await repository.ListAsync());
    }
}
