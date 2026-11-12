namespace MetadataAnalyzer.Core.Models;

public sealed record ConstructorModel(
    MemberVisibility Visibility = MemberVisibility.Public,
    IReadOnlyList<ParameterModel>? Parameters = null,
    bool IsStatic = false)
{
    public IReadOnlyList<ParameterModel> Parameters { get; init; } = Parameters ?? [];

    public string Signature => $"({string.Join(", ", Parameters.Select(parameter => parameter.DisplayName))})";

    public string Modifier => IsStatic ? "static " : string.Empty;
}
