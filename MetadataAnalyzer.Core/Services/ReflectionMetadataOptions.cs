namespace MetadataAnalyzer.Core.Services;

public sealed class ReflectionMetadataOptions
{
    public bool IncludeNestedTypes { get; set; } = true;

    public bool IncludeCompilerGeneratedMembers { get; set; }

    public bool ProbeAssemblyDirectory { get; set; } = true;

    public string? CoreAssemblyName { get; set; }

    public IReadOnlyList<string> ProbingPaths { get; set; } = [];
}
