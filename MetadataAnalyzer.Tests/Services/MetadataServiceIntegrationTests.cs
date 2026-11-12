using System.IO;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;
using MetadataAnalyzer.Core.Services;
using MetadataAnalyzer.Tests.Support;
using Moq;

namespace MetadataAnalyzer.Tests.Services;

public sealed class MetadataServiceIntegrationTests : IDisposable
{
    private readonly RecordingLogger _logger = new();
    private readonly TemporaryDirectory _directory = new();

    private static Mock<IMetadataProvider> ProviderReturning(AssemblyModel assembly)
    {
        var provider = new Mock<IMetadataProvider>();
        provider
            .Setup(item => item.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(assembly);
        return provider;
    }

    private XmlMetadataRepository CreateRepository() =>
        new(new XmlMetadataRepositoryOptions { DirectoryPath = _directory.Path });

    private static AssemblyModel Sample(string version = "1.0.0.0") =>
        MockMetadataProvider.BuildAssembly(DateTimeOffset.UtcNow) with { Version = version };

    [Fact]
    public async Task AnalyzeAndSaveAsyncStoresAnalysisInRepository()
    {
        var assembly = Sample();
        using var repository = CreateRepository();
        using var service = new MetadataService(ProviderReturning(assembly).Object, _logger, repository);

        var analyzed = await service.AnalyzeAndSaveAsync(new MetadataSource("SampleLibrary.dll"));

        Assert.Same(assembly, analyzed);
        Assert.Equal(["SampleLibrary"], await repository.ListAsync());
        Assert.Contains(_logger.Information, entry => entry.StartsWith(LogEvents.MetadataSaved, StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnalyzeAndSaveAsyncRemembersSourceForRefresh()
    {
        var assembly = Sample();
        var provider = ProviderReturning(assembly);
        using var repository = CreateRepository();
        using var service = new MetadataService(provider.Object, _logger, repository);
        var source = new MetadataSource("SampleLibrary.dll");

        await service.AnalyzeAndSaveAsync(source);

        Assert.Equal(source, service.Source);

        await service.RefreshAsync();

        provider.Verify(
            item => item.LoadAsync(It.Is<MetadataSource>(value => value.FilePath == source.FilePath), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RefreshAsyncReplacesStoredAnalysis()
    {
        using var repository = CreateRepository();
        using var service = new MetadataService(ProviderReturning(Sample("1.0.0.0")).Object, _logger, repository);
        var source = new MetadataSource("SampleLibrary.dll");

        await service.AnalyzeAndSaveAsync(source);

        using var refreshed = new MetadataService(ProviderReturning(Sample("2.0.0.0")).Object, _logger, repository);
        await refreshed.RefreshAsync(source);

        var stored = await repository.LoadAsync("SampleLibrary");

        Assert.NotNull(stored);
        Assert.Equal("2.0.0.0", stored.Version);
        Assert.Equal(["SampleLibrary"], await repository.ListAsync());
    }

    [Fact]
    public async Task RefreshAsyncReportsChangedAnalysis()
    {
        using var repository = CreateRepository();
        var provider = ProviderReturning(Sample("1.0.0.0"));
        using var service = new MetadataService(provider.Object, _logger, repository);

        await service.AnalyzeAsync(new MetadataSource("SampleLibrary.dll"));

        provider
            .Setup(item => item.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Sample("2.0.0.0"));

        await service.RefreshAsync();

        Assert.Contains(_logger.Information, entry => entry.StartsWith(LogEvents.MetadataUpdated, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RefreshAsyncReportsUnchangedAnalysis()
    {
        using var repository = CreateRepository();
        using var service = new MetadataService(ProviderReturning(Sample()).Object, _logger, repository);

        await service.AnalyzeAsync(new MetadataSource("SampleLibrary.dll"));
        await service.RefreshAsync();

        Assert.Contains(_logger.Information, entry => entry.StartsWith(LogEvents.MetadataUnchanged, StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsyncRestoresSavedAnalysis()
    {
        var assembly = Sample();
        using var repository = CreateRepository();
        using var service = new MetadataService(ProviderReturning(assembly).Object, _logger, repository);

        await service.AnalyzeAndSaveAsync(new MetadataSource("SampleLibrary.dll"));
        var loaded = await service.LoadAsync("SampleLibrary");

        Assert.NotNull(loaded);
        Assert.Empty(AssemblyModelComparer.Describe(assembly, loaded));
        Assert.Same(loaded, service.Current);
    }

    [Fact]
    public async Task LoadAsyncReturnsNullWhenAnalysisIsMissing()
    {
        using var repository = CreateRepository();
        using var service = new MetadataService(ProviderReturning(Sample()).Object, _logger, repository);

        Assert.Null(await service.LoadAsync("Unknown"));
        Assert.Contains(_logger.Warnings, entry => entry.StartsWith(LogEvents.MetadataNotFound, StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadLatestAsyncReturnsMostRecentAnalysis()
    {
        using var repository = CreateRepository();
        using var service = new MetadataService(ProviderReturning(Sample()).Object, _logger, repository);
        await service.SaveAsync(Sample() with { Name = "Older", LoadedAt = DateTimeOffset.UnixEpoch });
        await service.SaveAsync(Sample() with { Name = "Newer", LoadedAt = DateTimeOffset.UtcNow });

        var latest = await service.LoadLatestAsync();

        Assert.NotNull(latest);
        Assert.Equal("Newer", latest.Name);
        Assert.Same(latest, service.Current);
    }

    [Fact]
    public async Task DeleteAsyncRemovesAnalysisAndClearsCurrent()
    {
        using var repository = CreateRepository();
        using var service = new MetadataService(ProviderReturning(Sample()).Object, _logger, repository);
        await service.AnalyzeAndSaveAsync(new MetadataSource("SampleLibrary.dll"));

        Assert.True(await service.DeleteAsync("SampleLibrary"));
        Assert.False(await service.DeleteAsync("SampleLibrary"));
        Assert.Null(service.Current);
        Assert.Empty(await service.ListAsync());
    }

    [Fact]
    public async Task SaveAsyncUpdatesExistingAnalysis()
    {
        using var repository = CreateRepository();
        using var service = new MetadataService(ProviderReturning(Sample()).Object, _logger, repository);

        await service.SaveAsync(Sample("1.0.0.0"));
        await service.SaveAsync(Sample("3.1.4.0"));

        var stored = await service.LoadAsync("SampleLibrary");

        Assert.NotNull(stored);
        Assert.Equal("3.1.4.0", stored.Version);
        Assert.Single(await service.ListAsync());
    }

    [Fact]
    public async Task AnalyzeAsyncTranslatesMissingSource()
    {
        var provider = new Mock<IMetadataProvider>();
        provider
            .Setup(item => item.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("not found", "missing.dll"));

        using var service = new MetadataService(provider.Object, _logger);

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(
            () => service.AnalyzeAsync(new MetadataSource("missing.dll")));

        Assert.Equal(MetadataErrorCode.SourceNotFound, exception.Code);
        Assert.Equal("missing.dll", exception.FilePath);
        Assert.IsType<FileNotFoundException>(exception.InnerException);
    }

    [Fact]
    public async Task AnalyzeAsyncTranslatesCorruptedAssembly()
    {
        var provider = new Mock<IMetadataProvider>();
        provider
            .Setup(item => item.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidDataException("bad image"));

        using var service = new MetadataService(provider.Object, _logger);

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(() => service.AnalyzeAsync());

        Assert.Equal(MetadataErrorCode.InvalidAssembly, exception.Code);
        Assert.Contains(_logger.Errors, entry => entry.StartsWith(LogEvents.MetadataAnalysisFailed, StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsyncTranslatesCorruptedDocument()
    {
        using var repository = CreateRepository();
        using var service = new MetadataService(ProviderReturning(Sample()).Object, _logger, repository);
        await service.SaveAsync(Sample());
        await File.WriteAllTextAsync(Path.Combine(_directory.Path, "SampleLibrary.xml"), "<broken>");

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(() => service.LoadAsync("SampleLibrary"));

        Assert.Equal(MetadataErrorCode.RepositoryCorrupted, exception.Code);
        Assert.Equal("SampleLibrary", exception.AssemblyName);
        Assert.IsType<RepositoryDocumentException>(exception.InnerException);
    }

    [Fact]
    public async Task SaveAsyncTranslatesAccessDenied()
    {
        var repository = new Mock<IMetadataRepository>();
        repository
            .Setup(item => item.SaveAsync(It.IsAny<AssemblyModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("denied"));

        using var service = new MetadataService(ProviderReturning(Sample()).Object, _logger, repository.Object);

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(() => service.SaveAsync(Sample()));

        Assert.Equal(MetadataErrorCode.AccessDenied, exception.Code);
    }

    [Fact]
    public async Task AnalyzeAndSaveAsyncStopsWhenAnalysisIsCancelled()
    {
        var provider = new Mock<IMetadataProvider>();
        provider
            .Setup(item => item.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());
        using var repository = CreateRepository();
        using var service = new MetadataService(provider.Object, _logger, repository);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.AnalyzeAndSaveAsync(new MetadataSource("SampleLibrary.dll")));

        Assert.Empty(await repository.ListAsync());
    }

    [Fact]
    public async Task ServiceWorksWithAnyRepositoryImplementation()
    {
        var repository = new InMemoryMetadataRepository();
        using var service = new MetadataService(ProviderReturning(Sample()).Object, _logger, repository);

        await service.AnalyzeAndSaveAsync(new MetadataSource("SampleLibrary.dll"));

        Assert.Equal(["SampleLibrary"], await service.ListAsync());
    }

    [Fact]
    public async Task ListAsyncPropagatesRepositoryFailure()
    {
        var repository = new Mock<IMetadataRepository>();
        repository
            .Setup(item => item.ListAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("unavailable"));

        using var service = new MetadataService(ProviderReturning(Sample()).Object, _logger, repository.Object);

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(() => service.ListAsync());

        Assert.Equal(MetadataErrorCode.RepositoryUnavailable, exception.Code);
    }

    public void Dispose() => _directory.Dispose();
}
