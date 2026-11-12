using System.IO;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;
using MetadataAnalyzer.Core.Services;
using MetadataAnalyzer.Tests.Support;
using Moq;

namespace MetadataAnalyzer.Tests.Services;

public sealed class MetadataServiceTests
{
    private readonly RecordingLogger _logger = new();
    private readonly Mock<IMetadataProvider> _provider = new();

    [Fact]
    public async Task AnalyzeAsyncReturnsModelFromProvider()
    {
        var expected = new AssemblyModel("SampleLibrary", "1.0.0.0", "SampleLibrary.dll");
        _provider
            .Setup(provider => provider.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var service = CreateService();
        var actual = await service.AnalyzeAsync();

        Assert.Same(expected, actual);
        Assert.Same(expected, service.Current);
    }

    [Fact]
    public async Task AnalyzeAsyncLogsStartAndCompletion()
    {
        _provider
            .Setup(provider => provider.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AssemblyModel("SampleLibrary", "1.0.0.0", "SampleLibrary.dll"));

        await CreateService().AnalyzeAsync();

        Assert.Contains(LogEvents.MetadataAnalysisStarted, _logger.Information);
        Assert.Contains(_logger.Information, entry => entry.StartsWith(LogEvents.SampleDataLoaded, StringComparison.Ordinal));
        Assert.Empty(_logger.Errors);
    }

    [Fact]
    public async Task AnalyzeAsyncLogsErrorAndRethrowsWhenProviderFails()
    {
        _provider
            .Setup(provider => provider.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("assembly is corrupted"));

        var service = CreateService();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AnalyzeAsync());

        Assert.Equal("assembly is corrupted", exception.Message);
        Assert.Contains(_logger.Errors, entry => entry.Contains("assembly is corrupted", StringComparison.Ordinal));
        Assert.Null(service.Current);
    }

    [Fact]
    public async Task AnalyzeAsyncLogsWarningWhenCancelled()
    {
        _provider
            .Setup(provider => provider.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService().AnalyzeAsync());

        Assert.Contains(_logger.Warnings, entry => entry.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsyncPassesCancellationTokenToProvider()
    {
        using var cancellation = new CancellationTokenSource();
        _provider
            .Setup(provider => provider.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssemblyModel.Empty());

        await CreateService().AnalyzeAsync(cancellation.Token);

        _provider.Verify(provider => provider.LoadAsync(It.IsAny<MetadataSource>(), cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task RefreshAsyncReplacesCurrentModel()
    {
        var first = new AssemblyModel("First", "1.0.0.0", "First.dll");
        var second = new AssemblyModel("Second", "2.0.0.0", "Second.dll");
        _provider
            .SetupSequence(provider => provider.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(first)
            .ReturnsAsync(second);

        var service = CreateService();

        Assert.Equal(first, await service.AnalyzeAsync());
        Assert.Equal(second, await service.RefreshAsync());
        Assert.Same(second, service.Current);
    }

    [Fact]
    public async Task CurrentIsNullBeforeFirstAnalysis()
    {
        var service = CreateService();

        Assert.Null(service.Current);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task AnalyzeAsyncPassesSourceToProvider()
    {
        var source = new MetadataSource(@"C:\assemblies\SampleLibrary.dll");
        _provider
            .Setup(provider => provider.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssemblyModel.Empty());

        await CreateService().AnalyzeAsync(source);

        _provider.Verify(provider => provider.LoadAsync(source, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeAsyncWithoutSourceUsesEmptySource()
    {
        _provider
            .Setup(provider => provider.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssemblyModel.Empty());

        await CreateService().AnalyzeAsync();

        _provider.Verify(provider => provider.LoadAsync(MetadataSource.Empty, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveAsyncDelegatesToRepositoryAndLogs()
    {
        var repository = new InMemoryMetadataRepository();
        var assembly = new AssemblyModel("SampleLibrary", "1.0.0.0", "SampleLibrary.dll");

        await CreateService(repository).SaveAsync(assembly);

        Assert.Same(assembly, await repository.LoadAsync("SampleLibrary"));
        Assert.Contains(_logger.Information, entry => entry.StartsWith(LogEvents.MetadataSaved, StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsyncRestoresAssemblyFromRepository()
    {
        var repository = new InMemoryMetadataRepository();
        var assembly = new AssemblyModel("SampleLibrary", "1.0.0.0", "SampleLibrary.dll");
        await repository.SaveAsync(assembly);

        var service = CreateService(repository);
        var restored = await service.LoadAsync("SampleLibrary");

        Assert.Same(assembly, restored);
        Assert.Same(assembly, service.Current);
        Assert.Contains(_logger.Information, entry => entry.StartsWith(LogEvents.MetadataLoaded, StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadAsyncReturnsNullAndWarnsWhenAssemblyIsMissing()
    {
        var service = CreateService(new InMemoryMetadataRepository());

        Assert.Null(await service.LoadAsync("Missing"));
        Assert.Null(service.Current);
        Assert.Contains(_logger.Warnings, entry => entry.Contains(LogEvents.MetadataNotFound, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListAsyncReturnsRepositoryNames()
    {
        var repository = new InMemoryMetadataRepository();
        await repository.SaveAsync(new AssemblyModel("Beta", "1.0.0.0", "Beta.dll"));
        await repository.SaveAsync(new AssemblyModel("Alpha", "1.0.0.0", "Alpha.dll"));

        var names = await CreateService(repository).ListAsync();

        Assert.Equal(["Alpha", "Beta"], names);
    }

    [Fact]
    public async Task RepositoryOperationsFailWithoutRepository()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(AssemblyModel.Empty()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.LoadAsync("SampleLibrary"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ListAsync());
    }

    [Fact]
    public async Task SaveAsyncLogsAndRethrowsRepositoryFailure()
    {
        var repository = new Mock<IMetadataRepository>();
        repository
            .Setup(item => item.SaveAsync(It.IsAny<AssemblyModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("disk full"));

        var exception = await Assert.ThrowsAsync<IOException>(
            () => CreateService(repository.Object).SaveAsync(AssemblyModel.Empty()));

        Assert.Equal("disk full", exception.Message);
        Assert.Contains(_logger.Errors, entry => entry.StartsWith(LogEvents.MetadataRepositoryFailed, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveAsyncPropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var repository = new InMemoryMetadataRepository();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService(repository).SaveAsync(AssemblyModel.Empty(), cancellation.Token));
    }

    private MetadataService CreateService(IMetadataRepository? repository = null) =>
        new(_provider.Object, _logger, repository);
}
