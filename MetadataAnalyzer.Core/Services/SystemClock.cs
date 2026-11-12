namespace MetadataAnalyzer.Core.Services;

public sealed class SystemClock : Interfaces.ISystemClock
{
    public static SystemClock Instance { get; } = new();

    public DateTimeOffset Now => DateTimeOffset.Now;
}
