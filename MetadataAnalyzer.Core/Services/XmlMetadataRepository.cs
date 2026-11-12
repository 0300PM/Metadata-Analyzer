using System.Security;
using System.Text;
using System.Xml;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Services;

public sealed class XmlMetadataRepositoryOptions
{
    public string? DirectoryPath { get; set; }

    public string FileExtension { get; set; } = ".xml";

    public bool CreateDirectory { get; set; } = true;
}

public sealed class XmlMetadataRepository : IMetadataRepository, IDisposable
{
    private const string DefaultDirectoryName = "Repository";

    private const string RootElementName = RepositoryDocument.RootName;

    private static readonly string[] InvalidNameCharacters =
        [.. Path.GetInvalidFileNameChars().Select(character => character.ToString()), "+", "."];

    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly string _directoryPath;

    private readonly string _extension;

    private readonly bool _createDirectory;

    private readonly IAppLogger? _logger;

    public XmlMetadataRepository(XmlMetadataRepositoryOptions? options = null, IAppLogger? logger = null)
    {
        var settings = options ?? new XmlMetadataRepositoryOptions();
        var configured = settings.DirectoryPath;

        _directoryPath = string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(AppContext.BaseDirectory, DefaultDirectoryName)
            : Path.GetFullPath(configured, AppContext.BaseDirectory);
        _extension = NormalizeExtension(settings.FileExtension);
        _createDirectory = settings.CreateDirectory;
        _logger = logger;
    }

    public string Name => "xml";

    public string DirectoryPath => _directoryPath;

    public bool Exists => Directory.Exists(_directoryPath);

    public async Task SaveAsync(AssemblyModel assembly, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentException.ThrowIfNullOrWhiteSpace(assembly.Name);

        var path = GetPath(assembly.Name);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            EnsureDirectory();

            await WriteAtomicallyAsync(path, RepositoryDocumentMapper.ToDocument(assembly), cancellationToken)
                .ConfigureAwait(false);

            await LogAsync(LogEvents.MetadataSaved, path).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AssemblyModel?> LoadAsync(string assemblyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

        cancellationToken.ThrowIfCancellationRequested();

        var path = GetPath(assemblyName);

        if (!File.Exists(path))
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var assembly = await ReadAsync(path, cancellationToken).ConfigureAwait(false);

            await LogAsync(LogEvents.MetadataLoaded, path).ConfigureAwait(false);

            return assembly;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<AssemblyModel?> LoadLatestAsync(CancellationToken cancellationToken = default)
    {
        var names = await ListAsync(cancellationToken).ConfigureAwait(false);

        if (names.Count == 0)
        {
            return null;
        }

        AssemblyModel? latest = null;

        foreach (var name in names)
        {
            var assembly = await LoadAsync(name, cancellationToken).ConfigureAwait(false);

            if (assembly is not null && (latest is null || assembly.LoadedAt > latest.LoadedAt))
            {
                latest = assembly;
            }
        }

        return latest;
    }

    public async Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Exists)
        {
            return [];
        }

        try
        {
            return await Task.Run(
                    () => Directory
                        .EnumerateFiles(_directoryPath, "*" + _extension, SearchOption.TopDirectoryOnly)
                        .Where(IsRepositoryDocument)
                        .Select(path => Path.GetFileNameWithoutExtension(path))
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToArray(),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            await LogAsync($"{LogEvents.RepositoryUnavailable}: {exception.Message}", _directoryPath)
                .ConfigureAwait(false);

            throw new RepositoryDocumentException($"The repository directory could not be read: {_directoryPath}", exception);
        }
    }

    public async Task<bool> DeleteAsync(string assemblyName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assemblyName);

        var path = GetPath(assemblyName);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            await LogAsync($"{LogEvents.RepositoryWriteFailed}: {exception.Message}", path)
                .ConfigureAwait(false);

            throw new RepositoryDocumentException($"The analysis could not be deleted: {path}", exception);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<AssemblyModel> ReadAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            var document = await RepositoryDocumentSerializer.ReadAsync(stream, cancellationToken).ConfigureAwait(false);
            return RepositoryDocumentMapper.ToAssembly(document);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await LogAsync($"{LogEvents.RepositoryReadFailed}: {Describe(exception)}", path)
                .ConfigureAwait(false);

            throw new RepositoryDocumentException($"The analysis file could not be read: {path}", exception);
        }
    }

    private async Task WriteAtomicallyAsync(string path, RepositoryDocument document, CancellationToken cancellationToken)
    {
        var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                useAsync: true))
            {
                await RepositoryDocumentSerializer.WriteAsync(stream, document, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, path, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            await LogAsync($"{LogEvents.RepositoryWriteFailed}: {Describe(exception)}", path)
                .ConfigureAwait(false);

            throw new RepositoryDocumentException($"The analysis could not be saved: {path}", exception);
        }
        finally
        {
            DeleteQuietly(temporaryPath);
        }
    }

    private void EnsureDirectory()
    {
        if (Directory.Exists(_directoryPath))
        {
            return;
        }

        if (!_createDirectory)
        {
            throw new RepositoryDocumentException($"The repository directory does not exist: {_directoryPath}");
        }

        Directory.CreateDirectory(_directoryPath);
    }

    private string GetPath(string assemblyName) =>
        Path.Combine(_directoryPath, SanitizeName(assemblyName) + _extension);

    private static string SanitizeName(string assemblyName)
    {
        var builder = new StringBuilder(assemblyName.Length);

        foreach (var character in assemblyName)
        {
            builder.Append(char.IsControl(character) ? '_' : character);
        }

        var name = builder.ToString();

        foreach (var invalid in InvalidNameCharacters)
        {
            name = name.Replace(invalid, "_", StringComparison.Ordinal);
        }

        name = name.Trim();

        if (name.Length == 0 || name.All(character => character == '_'))
        {
            throw new RepositoryDocumentException($"The assembly name cannot be stored as a file: {assemblyName}");
        }

        return name;
    }

    private static bool IsRepositoryDocument(string path)
    {
        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });

            if (reader.MoveToContent() != XmlNodeType.Element)
            {
                return false;
            }

            return string.Equals(reader.LocalName, RootElementName, StringComparison.Ordinal);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or XmlException or SecurityException)
        {
            return false;
        }
    }

    public void Dispose() => _gate.Dispose();

    private async Task LogAsync(string message, string target)
    {
        if (_logger is null)
        {
            return;
        }

        try
        {
            await _logger.LogInformationAsync($"{message} ({target})").ConfigureAwait(false);
        }
        catch (Exception)
        {
        }
    }

    private static string Describe(Exception exception) =>
        exception.InnerException is { } inner
            ? $"{exception.GetType().Name}: {exception.Message} (inner {inner.GetType().Name}: {inner.Message})"
            : $"{exception.GetType().Name}: {exception.Message}";

    private static void DeleteQuietly(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".xml";
        }

        return extension.StartsWith('.') ? extension : "." + extension;
    }
}
