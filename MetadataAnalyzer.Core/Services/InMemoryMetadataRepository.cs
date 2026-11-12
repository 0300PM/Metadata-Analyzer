using System.Collections.Concurrent;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Services;

public sealed class InMemoryMetadataRepository : IMetadataRepository
{
    private readonly ConcurrentDictionary<string, AssemblyModel> _assemblies = new(StringComparer.Ordinal);

    public string Name => "memory";

    public Task SaveAsync(AssemblyModel assembly, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        cancellationToken.ThrowIfCancellationRequested();

        _assemblies[assembly.Name] = assembly;
        return Task.CompletedTask;
    }

    public Task<AssemblyModel?> LoadAsync(string assemblyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_assemblies.TryGetValue(assemblyName, out var assembly) ? assembly : null);
    }

    public Task<AssemblyModel?> LoadLatestAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            _assemblies.Values
                .OrderByDescending(assembly => assembly.LoadedAt)
                .ThenBy(assembly => assembly.Name, StringComparer.Ordinal)
                .FirstOrDefault());
    }

    public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<string> names = _assemblies.Keys.OrderBy(name => name, StringComparer.Ordinal).ToArray();
        return Task.FromResult(names);
    }

    public Task<bool> DeleteAsync(string assemblyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(_assemblies.TryRemove(assemblyName, out _));
    }
}
