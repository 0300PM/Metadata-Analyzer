using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Services;

public sealed class MetadataService(
    IMetadataProvider provider,
    IAppLogger logger,
    IMetadataRepository? repository = null) : IMetadataService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AssemblyModel? Current { get; private set; }

    public Task<AssemblyModel> AnalyzeAsync(CancellationToken cancellationToken = default) =>
        LoadAsync(MetadataSource.Empty, LogEvents.MetadataAnalysisStarted, cancellationToken);

    public Task<AssemblyModel> AnalyzeAsync(MetadataSource source, CancellationToken cancellationToken = default) =>
        LoadAsync(source, LogEvents.MetadataAnalysisStarted, cancellationToken);

    public Task<AssemblyModel> RefreshAsync(CancellationToken cancellationToken = default) =>
        LoadAsync(MetadataSource.Empty, LogEvents.MetadataRefreshStarted, cancellationToken);

    public async Task<AssemblyModel?> LoadAsync(string assemblyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

        var current = RequireRepository();

        try
        {
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
            await logger
                .LogErrorAsync($"{LogEvents.MetadataRepositoryFailed}: {exception.Message}")
                .ConfigureAwait(false);

            throw;
        }
    }

    public async Task SaveAsync(AssemblyModel assembly, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var current = RequireRepository();

        try
        {
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
            await logger
                .LogErrorAsync($"{LogEvents.MetadataRepositoryFailed}: {exception.Message}")
                .ConfigureAwait(false);

            throw;
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
            await logger
                .LogErrorAsync($"{LogEvents.MetadataRepositoryFailed}: {exception.Message}")
                .ConfigureAwait(false);

            throw;
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task<AssemblyModel> LoadAsync(
        MetadataSource source,
        string startMessage,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await logger.LogInformationAsync(startMessage).ConfigureAwait(false);

            var assembly = await provider.LoadAsync(source, cancellationToken).ConfigureAwait(false);
            Current = assembly;

            await logger
                .LogInformationAsync($"{LogEvents.SampleDataLoaded}: {assembly.Name} ({assembly.FileName})")
                .ConfigureAwait(false);

            return assembly;
        }
        catch (OperationCanceledException)
        {
            await logger.LogWarningAsync(LogEvents.MetadataAnalysisCancelled).ConfigureAwait(false);
            throw;
        }
        catch (Exception exception)
        {
            await logger
                .LogErrorAsync($"{LogEvents.MetadataAnalysisFailed}: {exception.Message}")
                .ConfigureAwait(false);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private IMetadataRepository RequireRepository() =>
        repository ?? throw new InvalidOperationException("No metadata repository is configured.");
}
