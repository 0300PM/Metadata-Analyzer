using System.Globalization;
using System.Text;
using MetadataAnalyzer.Core.Interfaces;
using Microsoft.Extensions.Options;

namespace MetadataAnalyzer.Infrastructure.Logging;

public sealed class FileLogger : IAppLogger, IDisposable
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly LoggingOptions _options;
    private readonly string _filePath;
    private readonly Action<string>? _writeToConsole;

    public FileLogger(IOptions<LoggingOptions> options, Action<string>? writeToConsole = null)
        : this(options.Value, writeToConsole)
    {
    }

    public FileLogger(LoggingOptions options, Action<string>? writeToConsole = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _filePath = Path.GetFullPath(options.FilePath);
        _writeToConsole = options.LogToConsole ? writeToConsole ?? Console.WriteLine : null;
    }

    public string FilePath => _filePath;

    public Task LogInformationAsync(string message) => WriteAsync("INFO", message);

    public Task LogWarningAsync(string message) => WriteAsync("WARN", message);

    public Task LogErrorAsync(string message) => WriteAsync("ERROR", message);

    public Task LogErrorAsync(string message, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return WriteAsync("ERROR", _options.IncludeExceptionDetails ? Describe(message, exception) : message);
    }

    private async Task WriteAsync(string level, string message)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var entry = Format(level, message);

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            WriteToConsole(entry);
            await AppendToFileAsync(entry).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void WriteToConsole(string entry)
    {
        try
        {
            _writeToConsole?.Invoke(entry);
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async Task AppendToFileAsync(string entry)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using var stream = new FileStream(
                _filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                bufferSize: 4096,
                useAsync: true);

            var bytes = Encoding.UTF8.GetBytes(entry + Environment.NewLine);
            await stream.WriteAsync(bytes).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            WriteToConsole($"[logging failure] {exception.GetType().Name}: {exception.Message}");
        }
    }

    public void Dispose() => _gate.Dispose();

    private static string Describe(string message, Exception exception)
    {
        var builder = new StringBuilder(message);

        for (var current = exception; current is not null; current = current.InnerException)
        {
            builder
                .Append(Environment.NewLine)
                .Append("        ")
                .Append(current.GetType().FullName)
                .Append(": ")
                .Append(current.Message);
        }

        return builder.ToString();
    }

    private static string Format(string level, string message) =>
        $"[{DateTimeOffset.Now.ToString(TimestampFormat, CultureInfo.InvariantCulture)}] {level,-5} {message}";
}
