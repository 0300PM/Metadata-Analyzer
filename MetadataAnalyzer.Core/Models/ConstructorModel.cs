namespace MetadataAnalyzer.Core.Models;

public sealed record ConstructorModel(
    MemberVisibility Visibility = MemberVisibility.Public,
    IReadOnlyList<ParameterModel>? Parameters = null)
{
    public IReadOnlyList<ParameterModel> Parameters { get; init; } = Parameters ?? [];

    public string Signature => $"({string.Join(", ", Parameters.Select(parameter => parameter.DisplayName))})";
}
