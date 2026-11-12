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
    IReadOnlyList<EnumMemberModel>? EnumMembers = null,
    IReadOnlyList<string>? Interfaces = null,
    bool IsSealed = false,
    bool IsGeneric = false,
    string? AssemblyName = null,
    IReadOnlyList<string>? GenericParameters = null,
    bool IsNested = false,
    string? DeclaringTypeName = null,
    bool IsFlagsEnum = false)
{
    public IReadOnlyList<MethodModel> Methods { get; init; } = Methods ?? [];

    public IReadOnlyList<PropertyModel> Properties { get; init; } = Properties ?? [];

    public IReadOnlyList<FieldModel> Fields { get; init; } = Fields ?? [];

    public IReadOnlyList<ConstructorModel> Constructors { get; init; } = Constructors ?? [];

    public IReadOnlyList<EnumMemberModel> EnumMembers { get; init; } = EnumMembers ?? [];

    public IReadOnlyList<string> Interfaces { get; init; } = Interfaces ?? [];

    public IReadOnlyList<string> GenericParameters { get; init; } = GenericParameters ?? [];

    public TypeKind TypeKind { get; init; } = IsInterface ? TypeKind.Interface : TypeKind;

    public string Namespace
    {
        get => NamespaceName ?? DeriveNamespace(FullName, Name);

        init => NamespaceName = value;
    }

    public string DisplayName => TypeKind switch
    {
        TypeKind.Interface => $"interface {Name}",
        TypeKind.Struct => $"struct {Name}",
        TypeKind.Enum => $"enum {Name}",
        TypeKind.Delegate => $"delegate {Name}",
        _ => Name,
    };

    public string KindLabel => TypeKind switch
    {
        TypeKind.Interface => "Interface",
        TypeKind.Struct => "Struct",
        TypeKind.Enum => "Enum",
        TypeKind.Delegate => "Delegate",
        _ => IsStatic ? "Static class" : "Class",
    };

    public int MemberCount => Methods.Count + Properties.Count + Fields.Count + Constructors.Count + EnumMembers.Count;

    public bool HasMembers => MemberCount > 0;

    public bool ImplementsInterfaces => Interfaces.Count > 0;

    public string GenericSuffix => GenericParameters.Count == 0
        ? string.Empty
        : $"<{string.Join(", ", GenericParameters)}>";

    public string QualifiedName => IsNested ? $"{DeclaringTypeName}+{Name}" : FullName;

    public string TypeSummary => string.IsNullOrWhiteSpace(BaseType)
        ? KindLabel
        : $"{KindLabel} : {BaseType}";

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
