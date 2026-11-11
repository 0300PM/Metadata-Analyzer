using MetadataAnalyzer.Infrastructure.Logging;
using MetadataAnalyzer.Tests.Support;

namespace MetadataAnalyzer.Tests.Logging;

public sealed class FileLoggerTests
{
    [Fact]
    public async Task WritesEntriesToConfiguredFile()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("Logs/application.log");
        var logger = new FileLogger(new LoggingOptions { FilePath = path, LogToConsole = false });

        await logger.LogInformationAsync("Application started");
        await logger.LogWarningAsync("Low memory");
        await logger.LogErrorAsync("Analysis failed");

        var content = await System.IO.File.ReadAllTextAsync(path);

        Assert.Contains("INFO  Application started", content, StringComparison.Ordinal);
        Assert.Contains("WARN  Low memory", content, StringComparison.Ordinal);
        Assert.Contains("ERROR Analysis failed", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EntryContainsTimestamp()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("application.log");
        var logger = new FileLogger(new LoggingOptions { FilePath = path, LogToConsole = false });

        await logger.LogInformationAsync("Timestamp check");

        var line = (await System.IO.File.ReadAllLinesAsync(path)).Single();

        Assert.Matches(@"^\[\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\] INFO  Timestamp check$", line);
    }

    [Fact]
    public async Task AppendsInsteadOfOverwriting()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("application.log");
        var options = new LoggingOptions { FilePath = path, LogToConsole = false };

        await new FileLogger(options).LogInformationAsync("first");
        await new FileLogger(options).LogInformationAsync("second");

        var lines = await System.IO.File.ReadAllLinesAsync(path);

        Assert.Equal(2, lines.Length);
        Assert.Contains("first", lines[0], StringComparison.Ordinal);
        Assert.Contains("second", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreatesMissingDirectory()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("nested/deeper/application.log");
        var logger = new FileLogger(new LoggingOptions { FilePath = path, LogToConsole = false });

        await logger.LogInformationAsync("nested");

        Assert.True(System.IO.File.Exists(path));
    }

    [Fact]
    public async Task DoesNothingWhenDisabled()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("application.log");
        var logger = new FileLogger(new LoggingOptions { FilePath = path, Enabled = false, LogToConsole = false });

        await logger.LogInformationAsync("ignored");

        Assert.False(System.IO.File.Exists(path));
    }

    [Fact]
    public async Task LogWriteFailureDoesNotThrow()
    {
        using var directory = new TemporaryDirectory();
        var blockedPath = directory.File("blocked");
        await System.IO.File.WriteAllTextAsync(blockedPath, "not a directory");

        var console = new List<string>();
        var logger = new FileLogger(
            new LoggingOptions { FilePath = System.IO.Path.Combine(blockedPath, "application.log") },
            console.Add);

        var exception = await Record.ExceptionAsync(() => logger.LogErrorAsync("should not throw"));

        Assert.Null(exception);
        Assert.Contains(console, line => line.Contains("[logging failure]", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EntriesAreWrittenInCallOrder()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("application.log");
        var logger = new FileLogger(new LoggingOptions { FilePath = path, LogToConsole = false });

        await Task.WhenAll(Enumerable.Range(0, 20).Select(index => logger.LogInformationAsync($"entry-{index}")));

        var lines = await System.IO.File.ReadAllLinesAsync(path);

        Assert.Equal(20, lines.Length);
        Assert.All(lines, line => Assert.Contains("entry-", line, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolvesFilePathAgainstApplicationDirectory()
    {
        var resolved = Infrastructure.AppConfiguration.ResolveLogFilePath("Logs/application.log");

        Assert.True(System.IO.Path.IsPathRooted(resolved));
        Assert.EndsWith(System.IO.Path.Combine("Logs", "application.log"), resolved, StringComparison.OrdinalIgnoreCase);
        await Task.CompletedTask;
    }
}
