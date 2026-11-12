using System.IO;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;
using MetadataAnalyzer.Core.Services;
using MetadataAnalyzer.Tests.Support;
using Moq;

namespace MetadataAnalyzer.Tests.Services;

public sealed class MetadataServiceErrorHandlingTests
{
    private readonly RecordingLogger _logger = new();

    private static MetadataService CreateService(Mock<IMetadataProvider> provider, RecordingLogger logger) =>
        new(provider.Object, logger);

    private static Mock<IMetadataProvider> ProviderThrowing(Exception exception)
    {
        var provider = new Mock<IMetadataProvider>();
        provider
            .Setup(item => item.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);
        return provider;
    }

    [Fact]
    public async Task MissingFileIsReportedAsSourceNotFound()
    {
        var provider = ProviderThrowing(new FileNotFoundException("not found", "C:/libs/missing.dll"));

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(
            () => CreateService(provider, _logger).AnalyzeAsync(new MetadataSource("C:/libs/missing.dll")));

        Assert.Equal(MetadataErrorCode.SourceNotFound, exception.Code);
        Assert.Contains("C:/libs/missing.dll", exception.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidPathIsReportedAsInvalidSource()
    {
        var provider = ProviderThrowing(new ArgumentException("The assembly path is not valid"));

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(
            () => CreateService(provider, _logger).AnalyzeAsync(new MetadataSource("::invalid::")));

        Assert.Equal(MetadataErrorCode.InvalidSource, exception.Code);
    }

    [Fact]
    public async Task TooLongPathIsReportedAsInvalidSource()
    {
        var provider = ProviderThrowing(new PathTooLongException("too long"));

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(
            () => CreateService(provider, _logger).AnalyzeAsync(new MetadataSource("long.dll")));

        Assert.Equal(MetadataErrorCode.InvalidSource, exception.Code);
    }

    [Fact]
    public async Task DeniedReadIsReportedAsAccessDenied()
    {
        var provider = ProviderThrowing(new UnauthorizedAccessException("denied"));

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(
            () => CreateService(provider, _logger).AnalyzeAsync(new MetadataSource("C:/libs/locked.dll")));

        Assert.Equal(MetadataErrorCode.AccessDenied, exception.Code);
        Assert.Contains("Access", exception.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnsupportedFormatIsReportedAsInvalidAssembly()
    {
        var provider = ProviderThrowing(new BadImageFormatException("bad header"));

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(
            () => CreateService(provider, _logger).AnalyzeAsync(new MetadataSource("C:/libs/notes.txt")));

        Assert.Equal(MetadataErrorCode.InvalidAssembly, exception.Code);
        Assert.Contains("not a valid .NET assembly", exception.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnsupportedFormatThroughNotSupportedIsReportedAsInvalidAssembly()
    {
        var provider = ProviderThrowing(new NotSupportedException("unsupported"));

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(
            () => CreateService(provider, _logger).AnalyzeAsync(new MetadataSource("C:/libs/native.dll")));

        Assert.Equal(MetadataErrorCode.InvalidAssembly, exception.Code);
    }

    [Fact]
    public async Task CorruptedAssemblyKeepsInnerException()
    {
        var inner = new BadImageFormatException("bad header");
        var provider = ProviderThrowing(new InvalidDataException("File is not a valid .NET assembly", inner));

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(
            () => CreateService(provider, _logger).AnalyzeAsync(new MetadataSource("C:/libs/broken.dll")));

        Assert.Equal(MetadataErrorCode.InvalidAssembly, exception.Code);
        Assert.Same(inner, exception.InnerException?.InnerException);
        Assert.Contains("InvalidDataException", _logger.Errors[0], StringComparison.Ordinal);
        Assert.Contains("BadImageFormatException", _logger.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingDependencyIsReportedSeparately()
    {
        var provider = ProviderThrowing(new FileLoadException("dependency missing"));

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(
            () => CreateService(provider, _logger).AnalyzeAsync(new MetadataSource("C:/libs/plugin.dll")));

        Assert.Equal(MetadataErrorCode.MissingDependency, exception.Code);
        Assert.Contains("references assemblies", exception.UserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnexpectedExceptionIsWrappedAndLogged()
    {
        var provider = ProviderThrowing(new TimeoutException("boom"));

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(
            () => CreateService(provider, _logger).AnalyzeAsync(new MetadataSource("C:/libs/odd.dll")));

        Assert.Equal(MetadataErrorCode.InvalidAssembly, exception.Code);
        Assert.IsType<TimeoutException>(exception.InnerException);
        Assert.False(string.IsNullOrWhiteSpace(exception.UserMessage));
    }

    [Fact]
    public async Task CancellationIsNotTreatedAsFailure()
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new Mock<IMetadataProvider>();
        provider
            .Setup(item => item.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .Returns(async (MetadataSource _, CancellationToken token) =>
            {
                await cancellation.CancelAsync();
                token.ThrowIfCancellationRequested();
                return AssemblyModel.Empty();
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService(provider, _logger).AnalyzeAsync(new MetadataSource("C:/libs/lib.dll"), cancellation.Token));

        Assert.Empty(_logger.Errors);
        Assert.Empty(_logger.Exceptions);
        Assert.Contains(_logger.Warnings, entry => entry == LogEvents.MetadataAnalysisCancelled);
    }

    [Fact]
    public async Task CancelledTokenStopsAnalysisBeforeProviderIsCalled()
    {
        var provider = new Mock<IMetadataProvider>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateService(provider, _logger).AnalyzeAsync(new MetadataSource("C:/libs/lib.dll"), cancellation.Token));
    }

    [Fact]
    public async Task AnalysisLogsStartCompletionAndCounters()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        var provider = new Mock<IMetadataProvider>();
        var assembly = MockMetadataProvider.BuildAssembly(clock.Now);

        provider
            .Setup(item => item.LoadAsync(It.IsAny<MetadataSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                clock.Advance(TimeSpan.FromMilliseconds(250));
                return assembly;
            });

        using var service = new MetadataService(provider.Object, _logger, clock: clock);
        await service.AnalyzeAsync(new MetadataSource("SampleLibrary.dll"));

        Assert.Contains(LogEvents.MetadataAnalysisStarted, _logger.Information);
        Assert.Contains(
            _logger.Information,
            entry => entry.StartsWith(LogEvents.MetadataAnalysisCompleted, StringComparison.Ordinal)
                && entry.Contains("250 ms", StringComparison.Ordinal)
                && entry.Contains($"{assembly.NamespaceCount} namespaces", StringComparison.Ordinal)
                && entry.Contains($"{assembly.ClassCount} types", StringComparison.Ordinal)
                && entry.Contains($"{assembly.MemberCount} members", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ErrorsAreLoggedWithExceptionInstance()
    {
        var provider = ProviderThrowing(new FileNotFoundException("not found", "missing.dll"));

        await Assert.ThrowsAsync<MetadataServiceException>(
            () => CreateService(provider, _logger).AnalyzeAsync(new MetadataSource("missing.dll")));

        Assert.Single(_logger.Exceptions);
        Assert.IsType<FileNotFoundException>(_logger.Exceptions[0]);
        Assert.Contains(_logger.Errors, entry => entry.Contains("missing.dll", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RepositoryReadFailureIsLoggedAsReadFailure()
    {
        var repository = new Mock<IMetadataRepository>();
        repository
            .Setup(item => item.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RepositoryDocumentException("damaged", new InvalidOperationException("bad xml")));

        using var service = new MetadataService(new Mock<IMetadataProvider>().Object, _logger, repository.Object);

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(() => service.LoadAsync("SampleLibrary"));

        Assert.Equal(MetadataErrorCode.RepositoryCorrupted, exception.Code);
        Assert.Contains(_logger.Information, entry => entry.StartsWith(LogEvents.MetadataLoadStarted, StringComparison.Ordinal));
        Assert.Contains(_logger.Errors, entry => entry.StartsWith(LogEvents.RepositoryReadFailed, StringComparison.Ordinal));
        Assert.Contains("bad xml", _logger.Errors[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RepositorySaveFailureIsLoggedAsWriteFailure()
    {
        var repository = new Mock<IMetadataRepository>();
        repository
            .Setup(item => item.SaveAsync(It.IsAny<AssemblyModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("read only"));

        using var service = new MetadataService(new Mock<IMetadataProvider>().Object, _logger, repository.Object);

        var exception = await Assert.ThrowsAsync<MetadataServiceException>(() => service.SaveAsync(AssemblyModel.Empty()));

        Assert.Equal(MetadataErrorCode.AccessDenied, exception.Code);
        Assert.Contains(_logger.Information, entry => entry.StartsWith(LogEvents.MetadataSaveStarted, StringComparison.Ordinal));
        Assert.Contains(_logger.Errors, entry => entry.StartsWith(LogEvents.RepositoryWriteFailed, StringComparison.Ordinal));
    }

    [Fact]
    public async Task UserMessageIsNeverEmpty()
    {
        foreach (var code in Enum.GetValues<MetadataErrorCode>())
        {
            var exception = new MetadataServiceException(code, "technical");

            Assert.False(string.IsNullOrWhiteSpace(exception.UserMessage));
        }
    }
}
