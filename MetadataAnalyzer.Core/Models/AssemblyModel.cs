namespace MetadataAnalyzer.Core.Models;

public sealed record AssemblyModel(
    string Name,
    string Version,
    string FileName,
    IReadOnlyList<NamespaceModel>? Namespaces = null,
    DateTimeOffset LoadedAt = default,
    string? FilePath = null,
    AssemblyLocation Location = AssemblyLocation.None,
    string? RuntimeVersion = null)
{
    public static AssemblyModel Empty(string name = "Empty") =>
        new(name, "0.0.0.0", string.Empty, []);

    public IReadOnlyList<NamespaceModel> Namespaces { get; init; } = Namespaces ?? [];

    public IEnumerable<ClassModel> Classes => Namespaces.SelectMany(@namespace => @namespace.Classes);

    public int NamespaceCount => Namespaces.Count;

    public int ClassCount => Classes.Count();

    public int MemberCount => Classes.Sum(@class => @class.MemberCount);

    public string? FullName { get; init; }

    public string Culture { get; init; } = "neutral";

    public string PublicKeyToken { get; init; } = string.Empty;

    public bool IsStrongNamed => PublicKeyToken.Length > 0;

    public bool HasFile => !string.IsNullOrWhiteSpace(FilePath);

    public bool IsLoaded => LoadedAt != default || HasFile;
}
