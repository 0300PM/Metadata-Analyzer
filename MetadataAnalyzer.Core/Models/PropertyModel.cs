namespace MetadataAnalyzer.Core.Models;

public sealed record PropertyModel(
    string Name,
    string PropertyType,
    bool CanRead = true,
    bool CanWrite = true,
    MemberVisibility Visibility = MemberVisibility.Public,
    bool IsStatic = false);
