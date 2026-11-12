using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Tests.Support;

public sealed class AssemblyModelComparer : IEqualityComparer<AssemblyModel?>
{
    public static AssemblyModelComparer Instance { get; } = new();

    public bool Equals(AssemblyModel? x, AssemblyModel? y) =>
        x is not null
        && y is not null
        && x.Name == y.Name
        && x.Version == y.Version
        && x.FileName == y.FileName
        && x.FilePath == y.FilePath
        && x.Location == y.Location
        && x.RuntimeVersion == y.RuntimeVersion
        && x.Culture == y.Culture
        && x.PublicKeyToken == y.PublicKeyToken
        && x.FullName == y.FullName
        && x.LoadedAt == y.LoadedAt
        && x.Namespaces.Count == y.Namespaces.Count
        && x.Namespaces.Zip(y.Namespaces).All(pair => Equals(pair.First, pair.Second));

    public static IReadOnlyList<string> Describe(AssemblyModel? expected, AssemblyModel? actual)
    {
        if (expected is null || actual is null)
        {
            return [expected is null ? "expected is null" : "loaded is null"];
        }

        var differences = new List<string>();

        if (expected.Name != actual.Name) differences.Add($"name '{expected.Name}' != '{actual.Name}'");
        if (expected.Version != actual.Version) differences.Add($"version");
        if (expected.FileName != actual.FileName) differences.Add($"file name '{expected.FileName}' != '{actual.FileName}'");
        if (expected.FilePath != actual.FilePath) differences.Add($"file path '{expected.FilePath}' != '{actual.FilePath}'");
        if (expected.Location != actual.Location) differences.Add($"location");
        if (expected.RuntimeVersion != actual.RuntimeVersion) differences.Add($"runtime version");
        if (expected.Culture != actual.Culture) differences.Add($"culture");
        if (expected.PublicKeyToken != actual.PublicKeyToken) differences.Add($"public key token");
        if (expected.FullName != actual.FullName) differences.Add($"assembly full name");
        if (expected.LoadedAt != actual.LoadedAt) differences.Add($"loaded at '{expected.LoadedAt:O}' != '{actual.LoadedAt:O}'");
        if (expected.Namespaces.Count != actual.Namespaces.Count) differences.Add($"namespace count {expected.Namespaces.Count} != {actual.Namespaces.Count}");

        if (!Instance.Equals(expected, actual) && differences.Count == 0)
        {
            differences.Add("assembly models differ");
        }

        foreach (var (left, right) in expected.Namespaces.Zip(actual.Namespaces))
        {
            if (left.Name != right.Name)
            {
                differences.Add($"namespace '{left.Name}' != '{right.Name}'");
            }

            if (left.Classes.Count != right.Classes.Count)
            {
                differences.Add($"type count of '{left.Name}' {left.Classes.Count} != {right.Classes.Count}");
            }

            if (!Equals(left, right) && differences.Count == 0)
            {
                differences.Add($"types of '{left.Name}' differ");
            }

            foreach (var (x, y) in left.Classes.Zip(right.Classes))
            {
                if (x.Namespace != y.Namespace) differences.Add($"namespace of {x.Name}: '{x.Namespace}' != '{y.Namespace}'");
                if (x.FullName != y.FullName) differences.Add($"full name of {x.Name}: '{x.FullName}' != '{y.FullName}'");
                if (x.BaseType != y.BaseType) differences.Add($"base type of {x.Name}: '{x.BaseType}' != '{y.BaseType}'");
                if (x.TypeKind != y.TypeKind) differences.Add($"kind of {x.Name}");
                if (x.Visibility != y.Visibility) differences.Add($"visibility of {x.Name}");
                if (x.AssemblyName != y.AssemblyName) differences.Add($"assembly of {x.Name}: '{x.AssemblyName}' != '{y.AssemblyName}'");
                if (x.DeclaringTypeName != y.DeclaringTypeName) differences.Add($"declaring type of {x.Name}");
                if (x.IsStatic != y.IsStatic) differences.Add($"static of {x.Name}");
                if (x.IsAbstract != y.IsAbstract) differences.Add($"abstract of {x.Name}");
                if (x.IsSealed != y.IsSealed) differences.Add($"sealed of {x.Name}");
                if (x.IsGeneric != y.IsGeneric) differences.Add($"generic of {x.Name}");
                if (x.IsNested != y.IsNested) differences.Add($"nested of {x.Name}");
                if (x.IsFlagsEnum != y.IsFlagsEnum) differences.Add($"flags of {x.Name}");
                if (!x.GenericParameters.SequenceEqual(y.GenericParameters)) differences.Add($"generic parameters of {x.Name}");
                if (!x.Interfaces.SequenceEqual(y.Interfaces)) differences.Add($"interfaces of {x.Name}");
                if (!x.EnumMembers.SequenceEqual(y.EnumMembers)) differences.Add($"enum members of {x.Name}");
                if (!x.Fields.SequenceEqual(y.Fields)) differences.Add($"fields of {x.Name}");
                if (!x.Properties.SequenceEqual(y.Properties)) differences.Add($"properties of {x.Name}");
                if (!x.Constructors.SequenceEqual(y.Constructors)) differences.Add($"constructors of {x.Name}");
                if (!x.Methods.SequenceEqual(y.Methods)) differences.Add($"methods of {x.Name}");
            }
        }

        return differences;
    }

    public static bool DeepEquals(AssemblyModel? x, AssemblyModel? y) => Instance.Equals(x, y);

    public int GetHashCode(AssemblyModel? obj) =>
        obj is null ? 0 : HashCode.Combine(obj.Name, obj.Version, obj.LoadedAt, obj.Namespaces.Count);

    private static bool Equals(NamespaceModel x, NamespaceModel y) =>
        x.Name == y.Name
        && x.Classes.Count == y.Classes.Count
        && x.Classes.Zip(y.Classes).All(pair => Equals(pair.First, pair.Second));

    private static bool Equals(ClassModel x, ClassModel y) =>
        x.Name == y.Name
        && x.FullName == y.FullName
        && x.Namespace == y.Namespace
        && x.BaseType == y.BaseType
        && x.TypeKind == y.TypeKind
        && x.Visibility == y.Visibility
        && x.AssemblyName == y.AssemblyName
        && x.DeclaringTypeName == y.DeclaringTypeName
        && x.IsStatic == y.IsStatic
        && x.IsAbstract == y.IsAbstract
        && x.IsSealed == y.IsSealed
        && x.IsGeneric == y.IsGeneric
        && x.IsNested == y.IsNested
        && x.IsFlagsEnum == y.IsFlagsEnum
        && x.GenericParameters.SequenceEqual(y.GenericParameters)
        && x.Interfaces.SequenceEqual(y.Interfaces)
        && x.EnumMembers.SequenceEqual(y.EnumMembers)
        && x.Fields.SequenceEqual(y.Fields)
        && x.Properties.SequenceEqual(y.Properties)
        && x.Constructors.SequenceEqual(y.Constructors)
        && x.Methods.SequenceEqual(y.Methods);
}
