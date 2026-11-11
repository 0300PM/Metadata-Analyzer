namespace MetadataAnalyzer.Core.Models;

public sealed record MethodModel(
    string Name,
    string ReturnType,
    MemberVisibility Visibility = MemberVisibility.Public,
    bool IsStatic = false,
    IReadOnlyList<ParameterModel>? Parameters = null)
{
    public IReadOnlyList<ParameterModel> Parameters { get; init; } = Parameters ?? [];

    public string Signature => $"{Name}({string.Join(", ", Parameters.Select(parameter => parameter.DisplayName))})";
}
