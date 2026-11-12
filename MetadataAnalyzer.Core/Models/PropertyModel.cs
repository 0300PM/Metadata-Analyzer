namespace MetadataAnalyzer.Core.Models;

public sealed record PropertyModel(
    string Name,
    string PropertyType,
    bool CanRead = true,
    bool CanWrite = true,
    MemberVisibility Visibility = MemberVisibility.Public,
    bool IsStatic = false,
    bool HasIndexer = false,
    MemberVisibility? SetterVisibility = null)
{
    public string Accessors => (CanRead, CanWrite) switch
    {
        (true, true) => "get; set;",
        (true, false) => "get;",
        (false, true) => "set;",
        _ => "none",
    };
}
