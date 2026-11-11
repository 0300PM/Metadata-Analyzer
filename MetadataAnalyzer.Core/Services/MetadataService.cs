using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Services;

public sealed class MetadataService(IMetadataProvider provider, IAppLogger logger) : IMetadataService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public AssemblyModel? Current { get; private set; }

    public Task<AssemblyModel> AnalyzeAsync(CancellationToken cancellationToken = default) =>
        LoadAsync(LogEvents.MetadataAnalysisStarted, cancellationToken);

    public Task<AssemblyModel> RefreshAsync(CancellationToken cancellationToken = default) =>
        LoadAsync("Metadata refresh started", cancellationToken);

    public void Dispose() => _gate.Dispose();

    private async Task<AssemblyModel> LoadAsync(string startMessage, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await logger.LogInformationAsync(startMessage).ConfigureAwait(false);

            var assembly = await provider.LoadAsync(cancellationToken).ConfigureAwait(false);
            Current = assembly;

            await logger
                .LogInformationAsync($"{LogEvents.SampleDataLoaded}: {assembly.Name} ({assembly.FileName})")
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
}
