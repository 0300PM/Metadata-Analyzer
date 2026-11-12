namespace MetadataAnalyzer.Core.Models;

public sealed record ParameterModel(
    string Name,
    string ParameterType,
    bool IsOptional = false,
    int Position = 0,
    string? DefaultValue = null,
    bool IsOut = false,
    bool IsIn = false,
    bool IsReference = false)
{
    public string Modifier => (IsOut, IsIn, IsReference) switch
    {
        (true, _, _) => "out ",
        (_, true, _) => "in ",
        (_, _, true) => "ref ",
        _ => string.Empty,
    };

    public string DisplayName => IsOptional
        ? $"{Modifier}{ParameterType} {Name} = {DefaultValue ?? "default"}"
        : $"{Modifier}{ParameterType} {Name}";
}
