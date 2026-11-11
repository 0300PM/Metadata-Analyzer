namespace MetadataAnalyzer.Core.Models;

public sealed record AssemblyModel(
    string Name,
    string Version,
    string FileName,
    IReadOnlyList<NamespaceModel>? Namespaces = null,
    DateTimeOffset LoadedAt = default)
{
    public static AssemblyModel Empty(string name = "Empty") =>
        new(name, "0.0.0.0", string.Empty, []);

    public IReadOnlyList<NamespaceModel> Namespaces { get; init; } = Namespaces ?? [];

    public IEnumerable<ClassModel> Classes => Namespaces.SelectMany(@namespace => @namespace.Classes);

    public int NamespaceCount => Namespaces.Count;

    public int ClassCount => Classes.Count();

    public int MemberCount => Classes.Sum(@class => @class.MemberCount);
}
