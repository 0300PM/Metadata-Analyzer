namespace MetadataAnalyzer.Core.Services;

public enum MetadataErrorCode
{
    Unknown,
    InvalidSource,
    SourceNotFound,
    InvalidAssembly,
    MissingDependency,
    RepositoryUnavailable,
    RepositoryCorrupted,
    AccessDenied,
    Cancelled,
}
