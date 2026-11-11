namespace MetadataAnalyzer.Core.Models;

public sealed record FieldModel(
    string Name,
    string FieldType,
    MemberVisibility Visibility = MemberVisibility.Private,
    bool IsStatic = false,
    bool IsReadOnly = false);
