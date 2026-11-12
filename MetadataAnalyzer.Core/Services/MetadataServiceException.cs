namespace MetadataAnalyzer.Core.Services;

public sealed class MetadataServiceException : Exception
{
    public MetadataServiceException(
        MetadataErrorCode code,
        string message,
        string? assemblyName = null,
        string? filePath = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Code = code;
        AssemblyName = assemblyName;
        FilePath = filePath;
    }

    public MetadataServiceException()
        : this(MetadataErrorCode.Unknown, string.Empty)
    {
    }

    public MetadataServiceException(string message)
        : this(MetadataErrorCode.Unknown, message)
    {
    }

    public MetadataServiceException(string message, Exception innerException)
        : this(MetadataErrorCode.Unknown, message, innerException: innerException)
    {
    }

    public MetadataErrorCode Code { get; }

    public string? AssemblyName { get; }

    public string? FilePath { get; }

    public string UserMessage => Code switch
    {
        MetadataErrorCode.InvalidSource => "Choose an assembly file to analyse.",
        MetadataErrorCode.SourceNotFound => $"The file was not found: {DescribeTarget()}",
        MetadataErrorCode.InvalidAssembly => $"{DescribeTarget()} is not a valid .NET assembly.",
        MetadataErrorCode.MissingDependency => $"{DescribeTarget()} references assemblies that are not available.",
        MetadataErrorCode.RepositoryCorrupted => $"The saved analysis of {DescribeTarget()} is damaged and cannot be read.",
        MetadataErrorCode.RepositoryUnavailable => "The analysis repository is not available. Check the repository path and permissions.",
        MetadataErrorCode.AccessDenied => $"Access to {DescribeTarget()} was denied.",
        MetadataErrorCode.Cancelled => "The operation was cancelled.",
        _ => "The operation failed. See the log for details.",
    };

    private string DescribeTarget() =>
        FilePath is { Length: > 0 } path
            ? path
            : AssemblyName is { Length: > 0 } name
                ? name
                : "the file";
}
