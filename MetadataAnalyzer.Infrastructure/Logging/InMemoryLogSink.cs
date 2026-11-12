using System.Collections.ObjectModel;
using System.Globalization;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Infrastructure.Configuration;

namespace MetadataAnalyzer.Infrastructure.Logging;

public sealed class LogEntry(DateTimeOffset timestamp, string level, string message)
{
    public DateTimeOffset Timestamp { get; } = timestamp;

    public string Level { get; } = level;

    public string Message { get; } = message;

    public string Date => Timestamp.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public string Time => Timestamp.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}

public sealed class InMemoryLogSink
{
    private const int Capacity = 2000;

    private readonly object _gate = new();

    public ObservableCollection<LogEntry> Entries { get; } = [];

    public void Add(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            Entries.Add(entry);

            while (Entries.Count > Capacity)
            {
                Entries.RemoveAt(0);
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            Entries.Clear();
        }
    }
}

public sealed class SettingsAwareLogger(LoggingOptions options, InMemoryLogSink sink, IAppLogger inner) : IAppLogger
{
    public Task LogInformationAsync(string message) => WriteAsync("Information", message, inner.LogInformationAsync);

    public Task LogWarningAsync(string message) => WriteAsync("Warning", message, inner.LogWarningAsync);

    public Task LogErrorAsync(string message) => WriteAsync("Error", message, inner.LogErrorAsync);

    public Task LogErrorAsync(string message, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return IsEnabled("Error")
            ? WriteAsync("Error", $"{message}: {exception.GetType().Name}: {exception.Message}", _ => inner.LogErrorAsync(message, exception))
            : Task.CompletedTask;
    }

    private Task WriteAsync(string level, string message, Func<string, Task> write)
    {
        if (!IsEnabled(level))
        {
            return Task.CompletedTask;
        }

        sink.Add(new LogEntry(DateTimeOffset.Now, level, message));
        return write(message);
    }

    private bool IsEnabled(string level) =>
        Rank(level) >= Rank(SettingsService.NormalizeLevel(options.MinimumLevel));

    private static int Rank(string level) => level switch
    {
        "Debug" => 0,
        "Information" => 1,
        "Warning" => 2,
        "Error" => 3,
        _ => 1,
    };
}
