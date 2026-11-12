using System.Security;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Services;

public sealed class MetadataService(
    IMetadataProvider provider,
    IAppLogger logger,
    IMetadataRepository? repository = null,
    ISystemClock? clock = null) : IMetadataService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AssemblyModel? Current { get; private set; }

    public MetadataSource Source { get; private set; } = MetadataSource.Empty;

    public Task<AssemblyModel> AnalyzeAsync(CancellationToken cancellationToken = default) =>
        AnalyzeAsync(MetadataSource.Empty, cancellationToken);

    public Task<AssemblyModel> AnalyzeAsync(MetadataSource source, CancellationToken cancellationToken = default) =>
        LoadAsync(source, LogEvents.MetadataAnalysisStarted, default, cancellationToken);

    public Task<AssemblyModel> RefreshAsync(CancellationToken cancellationToken = default) =>
        LoadAsync(Source, LogEvents.MetadataRefreshStarted, Current, cancellationToken);

    public async Task<AssemblyModel?> LoadAsync(string assemblyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

        var current = RequireRepository();

        try
        {
            await logger
                .LogInformationAsync($"{LogEvents.MetadataLoadStarted}: {assemblyName} ({current.Name})")
                .ConfigureAwait(false);

            var assembly = await current.LoadAsync(assemblyName, cancellationToken).ConfigureAwait(false);

            if (assembly is null)
            {
                await logger
                    .LogWarningAsync($"{LogEvents.MetadataNotFound}: {assemblyName}")
                    .ConfigureAwait(false);

                return null;
            }

            Current = assembly;

            await logger
                .LogInformationAsync($"{LogEvents.MetadataLoaded}: {assembly.Name} ({current.Name})")
                .ConfigureAwait(false);

            return assembly;
        }
        catch (OperationCanceledException)
        {
            await logger.LogWarningAsync("Metadata loading cancelled").ConfigureAwait(false);
            throw;
        }
        catch (Exception exception) when (exception is not ArgumentException)
        {
            await LogFailureAsync(exception, LogEvents.RepositoryReadFailed, assemblyName).ConfigureAwait(false);

            throw Translate(exception, MetadataErrorCode.RepositoryCorrupted, assemblyName: assemblyName);
        }
    }

    public async Task<AssemblyModel> AnalyzeAndSaveAsync(
        MetadataSource source,
        CancellationToken cancellationToken = default)
    {
        var assembly = await AnalyzeAsync(source, cancellationToken).ConfigureAwait(false);
        await SaveAsync(assembly, cancellationToken).ConfigureAwait(false);

        return assembly;
    }

    public async Task<AssemblyModel> RefreshAsync(MetadataSource source, CancellationToken cancellationToken = default)
    {
        var assembly = await LoadAsync(source, LogEvents.MetadataRefreshStarted, null, cancellationToken)
            .ConfigureAwait(false);

        await SaveAsync(assembly, cancellationToken).ConfigureAwait(false);

        return assembly;
    }

    public async Task SaveAsync(AssemblyModel assembly, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var current = RequireRepository();

        try
        {
            await logger
                .LogInformationAsync($"{LogEvents.MetadataSaveStarted}: {assembly.Name} ({current.Name})")
                .ConfigureAwait(false);

            await current.SaveAsync(assembly, cancellationToken).ConfigureAwait(false);

            await logger
                .LogInformationAsync($"{LogEvents.MetadataSaved}: {assembly.Name} ({current.Name})")
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await logger.LogWarningAsync("Metadata saving cancelled").ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await LogFailureAsync(exception, LogEvents.RepositoryWriteFailed, assembly.Name).ConfigureAwait(false);

            throw Translate(exception, MetadataErrorCode.RepositoryUnavailable, assembly.Name);
        }
    }

    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        var current = RequireRepository();

        try
        {
            return await current.ListAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await LogFailureAsync(exception, LogEvents.RepositoryUnavailable).ConfigureAwait(false);

            throw Translate(exception, MetadataErrorCode.RepositoryUnavailable);
        }
    }

    public async Task<bool> DeleteAsync(string assemblyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

        var current = RequireRepository();

        try
        {
            var deleted = await current.DeleteAsync(assemblyName, cancellationToken).ConfigureAwait(false);

            if (deleted && string.Equals(Current?.Name, assemblyName, StringComparison.Ordinal))
            {
                Current = null;
                Source = MetadataSource.Empty;
            }

            await logger
                .LogInformationAsync($"{LogEvents.MetadataDeleted}: {assemblyName} ({current.Name})")
                .ConfigureAwait(false);

            return deleted;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await LogFailureAsync(exception, LogEvents.RepositoryWriteFailed, assemblyName).ConfigureAwait(false);

            throw Translate(exception, MetadataErrorCode.RepositoryUnavailable, assemblyName);
        }
    }

    public async Task<AssemblyModel?> LoadLatestAsync(CancellationToken cancellationToken = default)
    {
        var current = RequireRepository();

        try
        {
            var assembly = await current.LoadLatestAsync(cancellationToken).ConfigureAwait(false);

            if (assembly is null)
            {
                await logger.LogWarningAsync(LogEvents.MetadataNotFound).ConfigureAwait(false);
                return null;
            }

            Current = assembly;

            await logger
                .LogInformationAsync($"{LogEvents.MetadataLoaded}: {assembly.Name} ({current.Name})")
                .ConfigureAwait(false);

            return assembly;
        }
        catch (OperationCanceledException)
        {
            await logger.LogWarningAsync("Metadata loading cancelled").ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await LogFailureAsync(exception, LogEvents.RepositoryReadFailed).ConfigureAwait(false);

            throw Translate(exception, MetadataErrorCode.RepositoryCorrupted);
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task<AssemblyModel> LoadAsync(
        MetadataSource source,
        string startMessage,
        AssemblyModel? previous,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await logger.LogInformationAsync(startMessage).ConfigureAwait(false);

            var current = clock ?? SystemClock.Instance;
            var startedAt = current.Now;
            var assembly = await provider.LoadAsync(source, cancellationToken).ConfigureAwait(false);
            var elapsed = current.Now - startedAt;

            Current = assembly;
            Source = source;

            await logger
                .LogInformationAsync($"{LogEvents.SampleDataLoaded}: {assembly.Name} ({assembly.FileName})")
                .ConfigureAwait(false);

            await logger
                .LogInformationAsync(
                    $"{LogEvents.MetadataAnalysisCompleted}: {assembly.Name} in {elapsed.TotalMilliseconds:0} ms, "
                    + $"{assembly.NamespaceCount} namespaces, {assembly.ClassCount} types, {assembly.MemberCount} members")
                .ConfigureAwait(false);

            await LogChangesAsync(previous, assembly).ConfigureAwait(false);

            return assembly;
        }
        catch (OperationCanceledException)
        {
            await logger.LogWarningAsync(LogEvents.MetadataAnalysisCancelled).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await LogFailureAsync(exception, LogEvents.MetadataAnalysisFailed, filePath: source.FilePath)
                .ConfigureAwait(false);

            throw Translate(exception, MetadataErrorCode.InvalidAssembly, filePath: source.FilePath);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool HasChanges(AssemblyModel? previous, AssemblyModel current) =>
        previous is null
        || previous.Version != current.Version
        || previous.ClassCount != current.ClassCount
        || previous.MemberCount != current.MemberCount;

    private async Task LogChangesAsync(AssemblyModel? previous, AssemblyModel current)
    {
        if (previous is null)
        {
            return;
        }

        var message = HasChanges(previous, current)
            ? $"{LogEvents.MetadataUpdated}: {current.Name} ({previous.ClassCount} -> {current.ClassCount} types)"
            : $"{LogEvents.MetadataUnchanged}: {current.Name}";

        try
        {
            await logger.LogInformationAsync(message).ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    private async Task LogFailureAsync(
        Exception exception,
        string message,
        string? assemblyName = null,
        string? filePath = null)
    {
        try
        {
            await logger
                .LogErrorAsync($"{message}: {Describe(exception)}", exception)
                .ConfigureAwait(false);

            if (assemblyName is { Length: > 0 } name)
            {
                await logger.LogErrorAsync($"{message}: assembly '{name}'").ConfigureAwait(false);
            }

            if (filePath is { Length: > 0 } path)
            {
                await logger.LogErrorAsync($"{message}: source '{path}'").ConfigureAwait(false);
            }
        }
        catch (Exception)
        {
        }
    }

    private static string Describe(Exception exception) =>
        exception.InnerException is { } inner
            ? $"{exception.GetType().Name}: {exception.Message} (inner {inner.GetType().Name}: {inner.Message})"
            : $"{exception.GetType().Name}: {exception.Message}";

    private static MetadataServiceException Translate(
        Exception exception,
        MetadataErrorCode fallback,
        string? assemblyName = null,
        string? filePath = null)
    {
        var code = exception switch
        {
            FileNotFoundException => MetadataErrorCode.SourceNotFound,
            DirectoryNotFoundException => MetadataErrorCode.SourceNotFound,
            BadImageFormatException => MetadataErrorCode.InvalidAssembly,
            InvalidDataException => MetadataErrorCode.InvalidAssembly,
            FileLoadException => MetadataErrorCode.MissingDependency,
            UnauthorizedAccessException => MetadataErrorCode.AccessDenied,
            SecurityException => MetadataErrorCode.AccessDenied,
            NotSupportedException => MetadataErrorCode.InvalidAssembly,
            PathTooLongException => MetadataErrorCode.InvalidSource,
            ArgumentException => MetadataErrorCode.InvalidSource,
            AssemblyAnalysisException => MetadataErrorCode.InvalidAssembly,
            RepositoryDocumentException => MetadataErrorCode.RepositoryCorrupted,
            _ => fallback,
        };

        return new MetadataServiceException(code, exception.Message, assemblyName, filePath, exception);
    }

    private IMetadataRepository RequireRepository() =>
        repository ?? throw new InvalidOperationException("No metadata repository is configured.");
}
