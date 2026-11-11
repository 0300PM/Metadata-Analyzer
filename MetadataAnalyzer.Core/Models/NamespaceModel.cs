namespace MetadataAnalyzer.Core.Models;

public sealed record NamespaceModel(string Name, IReadOnlyList<ClassModel>? Classes = null)
{
    public IReadOnlyList<ClassModel> Classes { get; init; } = Classes ?? [];
}
