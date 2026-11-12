using System.Globalization;
using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Services;

public static class RepositoryDocumentMapper
{
    public const string DateTimeFormat = "O";

    public static RepositoryDocument ToDocument(AssemblyModel assembly) =>
        new()
        {
            SchemaVersionValue = RepositoryDocument.SchemaVersion,
            SavedAt = FormatTimestamp(DateTimeOffset.UtcNow),
            Assembly = ToElement(assembly),
        };

    public static AssemblyModel ToAssembly(RepositoryDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return document.Assembly is null
            ? throw new RepositoryDocumentException("The document does not contain an assembly element.")
            : ToModel(document.Assembly);
    }

    public static AssemblyElement ToElement(AssemblyModel assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return new AssemblyElement
        {
            Name = assembly.Name,
            Version = assembly.Version,
            Culture = assembly.Culture,
            PublicKeyToken = assembly.PublicKeyToken,
            FullName = assembly.FullName,
            FileName = assembly.FileName,
            Location = assembly.Location,
            LoadedAt = FormatTimestamp(assembly.LoadedAt),
            RuntimeVersion = assembly.RuntimeVersion,
            FilePath = assembly.FilePath,
            Namespaces = [.. assembly.Namespaces.Select(ToElement)],
        };
    }

    public static AssemblyModel ToModel(AssemblyElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        return new AssemblyModel(
            element.Name,
            element.Version,
            element.FileName,
            [.. (element.Namespaces ?? []).Select(ToModel)],
            ParseTimestamp(element.LoadedAt),
            element.FilePath,
            element.Location,
            element.RuntimeVersion)
        {
            FullName = element.FullName,
            Culture = string.IsNullOrWhiteSpace(element.Culture) ? "neutral" : element.Culture,
            PublicKeyToken = element.PublicKeyToken ?? string.Empty,
        };
    }

    private static string ResolveNamespace(string fullName, string name, string? declaringType)
    {
        if (!string.IsNullOrWhiteSpace(declaringType))
        {
            var index = fullName.LastIndexOf('+');

            if (index > 0)
            {
                return fullName[..index];
            }
        }

        var suffix = "." + name;

        return fullName.Equals(name, StringComparison.Ordinal)
            ? string.Empty
            : fullName.EndsWith(suffix, StringComparison.Ordinal)
                ? fullName[..^suffix.Length]
                : fullName;
    }

    private static string FormatTimestamp(DateTimeOffset value) =>
        value.ToString(DateTimeFormat, CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseTimestamp(string? value) =>
        DateTimeOffset.TryParseExact(
            value,
            DateTimeFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var timestamp)
            ? timestamp
            : default;

    private static NamespaceElement ToElement(NamespaceModel @namespace) =>
        new()
        {
            Name = @namespace.Name,
            Types = [.. @namespace.Classes.Select(ToElement)],
        };

    private static NamespaceModel ToModel(NamespaceElement element)
    {
        var classes = new List<ClassModel>();

        foreach (var type in element.Types ?? [])
        {
            classes.Add(ToModel(type));
        }

        return new NamespaceModel(element.Name, classes);
    }

    private static TypeElement ToElement(ClassModel type) =>
        new()
        {
            Name = type.Name,
            FullName = type.FullName,
            Kind = type.TypeKind,
            Visibility = type.Visibility,
            BaseType = type.BaseType,
            AssemblyName = type.AssemblyName,
            DeclaringType = type.DeclaringTypeName,
            IsStatic = type.IsStatic,
            IsAbstract = type.IsAbstract,
            IsSealed = type.IsSealed,
            IsGeneric = type.IsGeneric,
            IsNested = type.IsNested,
            IsFlagsEnum = type.IsFlagsEnum,
            GenericParameters = [.. type.GenericParameters],
            Interfaces = [.. type.Interfaces],
            EnumMembers = [.. type.EnumMembers.Select(ToElement)],
            Fields = [.. type.Fields.Select(ToElement)],
            Properties = [.. type.Properties.Select(ToElement)],
            Constructors = [.. type.Constructors.Select(ToElement)],
            Methods = [.. type.Methods.Select(ToElement)],
        };

    private static ClassModel ToModel(TypeElement element) =>
        new(
            Name: element.Name,
            FullName: element.FullName,
            BaseType: element.BaseType,
            Methods: [.. (element.Methods ?? []).Select(ToModel)],
            Properties: [.. (element.Properties ?? []).Select(ToModel)],
            Fields: [.. (element.Fields ?? []).Select(ToModel)],
            Constructors: [.. (element.Constructors ?? []).Select(ToModel)],
            Visibility: element.Visibility,
            IsStatic: element.IsStatic,
            IsAbstract: element.IsAbstract,
            IsInterface: element.Kind == TypeKind.Interface,
            NamespaceName: ResolveNamespace(element.FullName, element.Name, element.DeclaringType),
            TypeKind: element.Kind,
            EnumMembers: [.. (element.EnumMembers ?? []).Select(ToModel)],
            Interfaces: element.Interfaces ?? [],
            IsSealed: element.IsSealed,
            IsGeneric: element.IsGeneric,
            AssemblyName: element.AssemblyName,
            GenericParameters: element.GenericParameters ?? [],
            IsNested: element.IsNested,
            DeclaringTypeName: element.DeclaringType,
            IsFlagsEnum: element.IsFlagsEnum);

    private static ClassModel CreateTypeLegacy(TypeElement element) =>
        new(
            element.Name,
            element.FullName,
            element.BaseType,
            [.. (element.Methods ?? []).Select(ToModel)],
            [.. (element.Properties ?? []).Select(ToModel)],
            [.. (element.Fields ?? []).Select(ToModel)],
            [.. (element.Constructors ?? []).Select(ToModel)],
            element.Visibility,
            element.IsStatic,
            element.IsAbstract,
            element.Kind == TypeKind.Interface,
            NamespaceName: ResolveNamespace(element.FullName, element.Name, element.DeclaringType),
            element.Kind,
            [.. (element.EnumMembers ?? []).Select(ToModel)],
            element.Interfaces ?? [],
            element.IsSealed,
            element.IsGeneric,
            element.AssemblyName,
            element.GenericParameters ?? [],
            element.IsNested,
            element.DeclaringType,
            element.IsFlagsEnum);

    private static EnumMemberElement ToElement(EnumMemberModel member) =>
        new()
        {
            Name = member.Name,
            Value = member.Value,
            NumericValue = member.NumericValue?.ToString(CultureInfo.InvariantCulture),
        };

    private static EnumMemberModel ToModel(EnumMemberElement element) =>
        new(element.Name, element.Value, ParseNumericValue(element.NumericValue));

    private static MemberVisibility? ParseVisibility(string? value) =>
        Enum.TryParse<MemberVisibility>(value, ignoreCase: false, out var visibility) ? visibility : null;

    private static long? ParseNumericValue(string? value) =>
        long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric) ? numeric : null;

    private static FieldElement ToElement(FieldModel field) =>
        new()
        {
            Name = field.Name,
            Type = field.FieldType,
            Visibility = field.Visibility,
            IsStatic = field.IsStatic,
            IsReadOnly = field.IsReadOnly,
            IsConstant = field.IsConstant,
        };

    private static FieldModel ToModel(FieldElement element) =>
        new(element.Name, element.Type, element.Visibility, element.IsStatic, element.IsReadOnly, element.IsConstant);

    private static PropertyElement ToElement(PropertyModel property) =>
        new()
        {
            Name = property.Name,
            Type = property.PropertyType,
            Visibility = property.Visibility,
            SetterVisibility = property.SetterVisibility?.ToString(),
            CanRead = property.CanRead,
            CanWrite = property.CanWrite,
            IsStatic = property.IsStatic,
            HasIndexer = property.HasIndexer,
        };

    private static PropertyModel ToModel(PropertyElement element) =>
        new(
            element.Name,
            element.Type,
            element.CanRead,
            element.CanWrite,
            element.Visibility,
            element.IsStatic,
            element.HasIndexer,
            ParseVisibility(element.SetterVisibility));

    private static ConstructorElement ToElement(ConstructorModel constructor) =>
        new()
        {
            Visibility = constructor.Visibility,
            IsStatic = constructor.IsStatic,
            Parameters = [.. constructor.Parameters.Select(ToElement)],
        };

    private static ConstructorModel ToModel(ConstructorElement element) =>
        new(element.Visibility, [.. (element.Parameters ?? []).Select(ToModel)], element.IsStatic);

    private static MethodElement ToElement(MethodModel method) =>
        new()
        {
            Name = method.Name,
            ReturnType = method.ReturnType,
            Visibility = method.Visibility,
            IsStatic = method.IsStatic,
            IsAbstract = method.IsAbstract,
            IsVirtual = method.IsVirtual,
            IsOverride = method.IsOverride,
            IsExtension = method.IsExtension,
            GenericParameters = [.. method.GenericParameters],
            Parameters = [.. method.Parameters.Select(ToElement)],
        };

    private static MethodModel ToModel(MethodElement element) =>
        new(
            element.Name,
            element.ReturnType,
            element.Visibility,
            element.IsStatic,
            [.. (element.Parameters ?? []).Select(ToModel)],
            element.IsAbstract,
            element.IsVirtual,
            element.IsOverride,
            element.IsExtension,
            element.GenericParameters ?? []);

    private static ParameterElement ToElement(ParameterModel parameter) =>
        new()
        {
            Name = parameter.Name,
            Type = parameter.ParameterType,
            Position = parameter.Position,
            IsOptional = parameter.IsOptional,
            IsOut = parameter.IsOut,
            IsIn = parameter.IsIn,
            IsReference = parameter.IsReference,
            DefaultValue = parameter.DefaultValue,
        };

    private static ParameterModel ToModel(ParameterElement element) =>
        new(
            element.Name,
            element.Type,
            element.IsOptional,
            element.Position,
            element.DefaultValue,
            element.IsOut,
            element.IsIn,
            element.IsReference);
}
