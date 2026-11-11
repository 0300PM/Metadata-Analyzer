namespace MetadataAnalyzer.Core.Models;

public sealed record ClassModel(
    string Name,
    string FullName,
    string? BaseType = null,
    IReadOnlyList<MethodModel>? Methods = null,
    IReadOnlyList<PropertyModel>? Properties = null,
    IReadOnlyList<FieldModel>? Fields = null,
    IReadOnlyList<ConstructorModel>? Constructors = null,
    MemberVisibility Visibility = MemberVisibility.Public,
    bool IsStatic = false,
    bool IsAbstract = false,
    bool IsInterface = false,
    string? NamespaceName = null,
    TypeKind TypeKind = TypeKind.Class,
    IReadOnlyList<EnumMemberModel>? EnumMembers = null)
{
    public IReadOnlyList<MethodModel> Methods { get; init; } = Methods ?? [];

    public IReadOnlyList<PropertyModel> Properties { get; init; } = Properties ?? [];

    public IReadOnlyList<FieldModel> Fields { get; init; } = Fields ?? [];

    public IReadOnlyList<ConstructorModel> Constructors { get; init; } = Constructors ?? [];

    public IReadOnlyList<EnumMemberModel> EnumMembers { get; init; } = EnumMembers ?? [];

    public TypeKind TypeKind { get; init; } = IsInterface ? TypeKind.Interface : TypeKind;

    public string Namespace
    {
        get => NamespaceName ?? DeriveNamespace(FullName, Name);

        init => NamespaceName = value;
    }

    public string DisplayName => TypeKind == TypeKind.Interface ? $"interface {Name}" : Name;

    public int MemberCount => Methods.Count + Properties.Count + Fields.Count + Constructors.Count + EnumMembers.Count;

    public bool HasMembers => MemberCount > 0;

    private static string DeriveNamespace(string fullName, string name)
    {
        var suffix = "." + name;

        return fullName.Equals(name, StringComparison.Ordinal)
            ? string.Empty
            : fullName.EndsWith(suffix, StringComparison.Ordinal)
                ? fullName[..^suffix.Length]
                : fullName;
    }
}
