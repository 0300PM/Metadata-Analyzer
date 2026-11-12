using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MetadataAnalyzer.Core.Services;

public sealed class AssemblyAnalysisException : Exception
{
    public AssemblyAnalysisException(string message, string filePath, Exception? innerException = null)
        : base(message, innerException) => FilePath = filePath;

    public AssemblyAnalysisException()
        : this(string.Empty, string.Empty)
    {
    }

    public AssemblyAnalysisException(string message)
        : this(message, string.Empty)
    {
    }

    public AssemblyAnalysisException(string message, Exception innerException)
        : this(message, string.Empty, innerException)
    {
    }

    public string FilePath { get; }
}

public sealed class MetadataProviderResolver(IServiceProvider services, MetadataProviderOptions options) : IMetadataProvider
{
    public Task<Models.AssemblyModel> LoadAsync(CancellationToken cancellationToken = default) =>
        LoadAsync(new MetadataSource(options.FilePath), cancellationToken);

    public Task<Models.AssemblyModel> LoadAsync(MetadataSource source, CancellationToken cancellationToken = default)
    {
        var filePath = source.FilePath ?? options.FilePath;

        if (options.UseMockData && filePath is null)
        {
            return services.GetRequiredService<MockMetadataProvider>().LoadAsync(source, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException(
                "Real metadata analysis requires an assembly file path. Set Application:AssemblyPath or provide a source.");
        }

        return LoadFromFileAsync(services.GetRequiredService<ReflectionMetadataProvider>(), filePath, cancellationToken);
    }

    private static async Task<Models.AssemblyModel> LoadFromFileAsync(
        ReflectionMetadataProvider provider,
        string filePath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await provider.LoadAsync(new MetadataSource(filePath), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new AssemblyAnalysisException(
                $"Assembly could not be analysed: {exception.Message}",
                filePath,
                exception);
        }
    }
}
