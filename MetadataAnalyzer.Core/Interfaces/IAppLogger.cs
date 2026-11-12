namespace MetadataAnalyzer.Core.Interfaces;

public interface IAppLogger
{
    Task LogInformationAsync(string message);

    Task LogWarningAsync(string message);

    Task LogErrorAsync(string message);

    Task LogErrorAsync(string message, Exception exception) => LogErrorAsync($"{message}: {exception.Message}");
}
