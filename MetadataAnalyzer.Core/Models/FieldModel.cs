namespace MetadataAnalyzer.Core.Models;

public sealed record FieldModel(
    string Name,
    string FieldType,
    MemberVisibility Visibility = MemberVisibility.Private,
    bool IsStatic = false,
    bool IsReadOnly = false,
    bool IsConstant = false)
{
    public string Modifier => (IsConstant, IsStatic, IsReadOnly) switch
    {
        (true, _, _) => "const",
        (_, true, true) => "static readonly",
        (_, true, _) => "static",
        (_, _, true) => "readonly",
        _ => "instance",
    };
}
