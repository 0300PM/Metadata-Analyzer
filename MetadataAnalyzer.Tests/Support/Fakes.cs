using MetadataAnalyzer.Core.Interfaces;

namespace MetadataAnalyzer.Tests.Support;

internal sealed class FakeClock(DateTimeOffset now) : ISystemClock
{
    public DateTimeOffset Now { get; set; } = now;

    public void Advance(TimeSpan delta) => Now += delta;
}

internal sealed class RecordingLogger : IAppLogger
{
    public List<string> Information { get; } = [];

    public List<string> Warnings { get; } = [];

    public List<string> Errors { get; } = [];

    public Task LogInformationAsync(string message)
    {
        Information.Add(message);
        return Task.CompletedTask;
    }

    public Task LogWarningAsync(string message)
    {
        Warnings.Add(message);
        return Task.CompletedTask;
    }

    public Task LogErrorAsync(string message)
    {
        Errors.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class TemporaryDirectory : IDisposable
{
    public TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "metadata-analyzer-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Path))
        {
            System.IO.Directory.Delete(Path, recursive: true);
        }
    }
}
