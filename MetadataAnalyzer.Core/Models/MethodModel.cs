namespace MetadataAnalyzer.Core.Models;

public sealed record MethodModel(
    string Name,
    string ReturnType,
    MemberVisibility Visibility = MemberVisibility.Public,
    bool IsStatic = false,
    IReadOnlyList<ParameterModel>? Parameters = null,
    bool IsAbstract = false,
    bool IsVirtual = false,
    bool IsOverride = false,
    bool IsExtension = false,
    IReadOnlyList<string>? GenericParameters = null)
{
    public IReadOnlyList<ParameterModel> Parameters { get; init; } = Parameters ?? [];

    public IReadOnlyList<string> GenericParameters { get; init; } = GenericParameters ?? [];

    public bool IsGeneric => GenericParameters.Count > 0;

    public string Signature =>
        $"{Name}{DescribeGenerics()}({string.Join(", ", Parameters.Select(parameter => parameter.DisplayName))})";

    private string DescribeGenerics() => IsGeneric ? $"<{string.Join(", ", GenericParameters)}>" : string.Empty;

    public string Modifier => (IsStatic, IsAbstract, IsVirtual, IsOverride, IsExtension) switch
    {
        (true, _, _, _, _) => "static",
        (_, true, _, _, _) => "abstract",
        (_, _, true, _, _) => "virtual",
        (_, _, _, true, _) => "override",
        (_, _, _, _, true) => "extension",
        _ => "instance",
    };
}
