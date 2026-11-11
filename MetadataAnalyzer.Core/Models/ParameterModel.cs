namespace MetadataAnalyzer.Core.Models;

public sealed record ParameterModel(string Name, string ParameterType, bool IsOptional = false)
{
    public string DisplayName => IsOptional ? $"{ParameterType} {Name} = default" : $"{ParameterType} {Name}";
}
