namespace MetadataAnalyzer.Core.Models;

public sealed record EnumMemberModel(string Name, string? Value = null, long? NumericValue = null);
