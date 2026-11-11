namespace MetadataAnalyzer.Core.Interfaces;

public interface ISystemClock
{
    DateTimeOffset Now { get; }
}
