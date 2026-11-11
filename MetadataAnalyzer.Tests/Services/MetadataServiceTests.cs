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
            .Setup(provider => provider.LoadAsync(It.IsAny<CancellationToken>()))
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
            .Setup(provider => provider.LoadAsync(It.IsAny<CancellationToken>()))
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
            .Setup(provider => provider.LoadAsync(It.IsAny<CancellationToken>()))
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
            .Setup(provider => provider.LoadAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService().AnalyzeAsync());

        Assert.Contains(_logger.Warnings, entry => entry.Contains("cancelled", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task AnalyzeAsyncPassesCancellationTokenToProvider()
    {
        using var cancellation = new CancellationTokenSource();
        _provider
            .Setup(provider => provider.LoadAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssemblyModel.Empty());

        await CreateService().AnalyzeAsync(cancellation.Token);

        _provider.Verify(provider => provider.LoadAsync(cancellation.Token), Times.Once);
    }

    [Fact]
    public async Task RefreshAsyncReplacesCurrentModel()
    {
        var first = new AssemblyModel("First", "1.0.0.0", "First.dll");
        var second = new AssemblyModel("Second", "2.0.0.0", "Second.dll");
        _provider
            .SetupSequence(provider => provider.LoadAsync(It.IsAny<CancellationToken>()))
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

    private MetadataService CreateService() => new(_provider.Object, _logger);
}
