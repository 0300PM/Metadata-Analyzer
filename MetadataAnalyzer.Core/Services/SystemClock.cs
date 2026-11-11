namespace MetadataAnalyzer.Core.Services;

public sealed class SystemClock : Interfaces.ISystemClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
