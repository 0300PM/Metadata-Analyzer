using System.Globalization;
using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.WPF.ViewModels;

public sealed record DetailsSection(string Title, IReadOnlyList<DetailsItem> Items)
{
    public bool HasItems => Items.Count > 0;
}

public sealed record DetailsItem(string Name, string Type, string Summary);

public sealed class DetailsViewModel
{
    private DetailsViewModel(
        string title,
        string subtitle,
        string kind,
        IReadOnlyList<DetailsItem> properties,
        IReadOnlyList<DetailsSection> sections)
    {
        Title = title;
        Subtitle = subtitle;
        Kind = kind;
        Properties = properties;
        Sections = sections;
    }

    public static DetailsViewModel Empty { get; } = new(
        "No selection",
        "Select an element in Assembly Explorer to see its details.",
        string.Empty,
        [],
        []);

    public string Title { get; }

    public string Subtitle { get; }

    public string Kind { get; }

    public IReadOnlyList<DetailsItem> Properties { get; }

    public IReadOnlyList<DetailsSection> Sections { get; }

    public bool HasSelection => Kind.Length > 0;

    public static DetailsViewModel FromAssembly(AssemblyModel assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return new DetailsViewModel(
            assembly.Name,
            assembly.FileName,
            "Assembly",
            [
                new DetailsItem("Name", assembly.Name, "Assembly name"),
                new DetailsItem("Version", assembly.Version, "Assembly version"),
                new DetailsItem("File Name", assembly.FileName, "Loaded file"),
                new DetailsItem("Loaded", FormatTimestamp(assembly.LoadedAt), "Analysis timestamp"),
                new DetailsItem("Namespaces", Describe(assembly.NamespaceCount), "Namespace count"),
                new DetailsItem("Types", Describe(assembly.ClassCount), "Type count"),
                new DetailsItem("Members", Describe(assembly.MemberCount), "Total member count"),
            ],
            [
                new DetailsSection(
                    "Namespaces",
                    assembly.Namespaces
                        .Select(@namespace => new DetailsItem(
                            @namespace.Name,
                            Describe(@namespace.Classes.Count) + " types",
                            string.Join(", ", @namespace.Classes.Select(@class => @class.Name))))
                        .ToList()),
            ]);
    }

    public static DetailsViewModel FromNamespace(NamespaceModel @namespace)
    {
        ArgumentNullException.ThrowIfNull(@namespace);

        return new DetailsViewModel(
            @namespace.Name,
            "Namespace",
            "Namespace",
            [
                new DetailsItem("Name", @namespace.Name, "Fully qualified name"),
                new DetailsItem("Types", Describe(@namespace.Classes.Count), "Type count"),
                new DetailsItem("Members", Describe(@namespace.Classes.Sum(@class => @class.MemberCount)), "Total members"),
            ],
            [
                new DetailsSection(
                    "Types",
                    @namespace.Classes
                        .Select(@class => new DetailsItem(
                            @class.Name,
                            DescribeKind(@class.TypeKind),
                            Pluralize(@class.MemberCount, "member")))
                        .ToList()),
            ]);
    }

    public static DetailsViewModel FromClass(ClassModel type)
    {
        ArgumentNullException.ThrowIfNull(type);

        var properties = new List<DetailsItem>
        {
            new("Name", type.Name, "Short name"),
            new("Full Name", type.FullName, "Namespace qualified name"),
            new("Namespace", type.Namespace.Length == 0 ? "(global)" : type.Namespace, "Declaring namespace"),
            new("Type", DescribeKind(type.TypeKind), "Type kind"),
            new("Accessibility", type.Visibility.ToString(), "Access modifier"),
            new("Abstract", FormatBoolean(type.IsAbstract), "Abstract modifier"),
            new("Static", FormatBoolean(type.IsStatic), "Static modifier"),
            new("Members", Describe(type.MemberCount), "Total member count"),
        };

        if (type.BaseType is not null)
        {
            properties.Add(new DetailsItem("Base Type", type.BaseType, "Inherited type"));
        }

        var sections = new List<DetailsSection>();

        if (type.TypeKind == TypeKind.Enum)
        {
            sections.Add(new DetailsSection(
                "Values",
                type.EnumMembers
                    .Select(member => new DetailsItem(member.Name, member.Value ?? "?", "Enum value"))
                    .ToList()));
        }

        sections.Add(new DetailsSection(
            "Properties",
            type.Properties
                .Select(property => new DetailsItem(
                    property.Name,
                    property.PropertyType,
                    $"{property.Visibility} · {DescribeAccessors(property)}"))
                .ToList()));

        sections.Add(new DetailsSection(
            "Methods",
            type.Methods
                .Select(method => new DetailsItem(
                    method.Name,
                    method.ReturnType,
                    $"{method.Signature} · {DescribeFlags(method.IsStatic)}"))
                .ToList()));

        sections.Add(new DetailsSection(
            "Fields",
            type.Fields
                .Select(field => new DetailsItem(
                    field.Name,
                    field.FieldType,
                    $"{field.Visibility} · {DescribeFlags(field.IsStatic, field.IsReadOnly)}"))
                .ToList()));

        sections.Add(new DetailsSection(
            "Constructors",
            type.Constructors
                .Select(constructor => new DetailsItem(
                    constructor.Signature,
                    constructor.Visibility.ToString(),
                    Pluralize(constructor.Parameters.Count, "parameter")))
                .ToList()));

        return new DetailsViewModel(type.Name, type.FullName, DescribeKind(type.TypeKind), properties, sections);
    }

    public static DetailsViewModel FromMethod(ClassModel type, MethodModel method)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(method);

        return new DetailsViewModel(
            method.Name,
            $"{type.FullName}.{method.Signature}",
            "Method",
            [
                new DetailsItem("Name", method.Name, "Short name"),
                new DetailsItem("Return Type", method.ReturnType, "Declared return type"),
                new DetailsItem("Signature", method.Signature, "Name and parameters"),
                new DetailsItem("Accessibility", method.Visibility.ToString(), "Access modifier"),
                new DetailsItem("Static", FormatBoolean(method.IsStatic), "Static member"),
                new DetailsItem(
                    "Parameters",
                    method.Parameters.Count == 0
                        ? "(none)"
                        : string.Join(", ", method.Parameters.Select(parameter => $"{parameter.Name}: {parameter.ParameterType}")),
                    "Parameter list"),
            ],
            [
                new DetailsSection(
                    "Parameters",
                    method.Parameters
                        .Select(parameter => new DetailsItem(
                            parameter.Name,
                            parameter.ParameterType,
                            parameter.IsOptional ? "optional" : "required"))
                        .ToList()),
            ]);
    }

    public static DetailsViewModel FromProperty(ClassModel type, PropertyModel property)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(property);

        return new DetailsViewModel(
            property.Name,
            $"{type.FullName}.{property.Name}",
            "Property",
            [
                new DetailsItem("Name", property.Name, "Short name"),
                new DetailsItem("Type", property.PropertyType, "Declared property type"),
                new DetailsItem("Accessibility", property.Visibility.ToString(), "Access modifier"),
                new DetailsItem("Accessors", DescribeAccessors(property), "Supported accessors"),
                new DetailsItem("Static", FormatBoolean(property.IsStatic), "Static member"),
            ],
            []);
    }

    public static DetailsViewModel FromField(ClassModel type, FieldModel field)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(field);

        return new DetailsViewModel(
            field.Name,
            $"{type.FullName}.{field.Name}",
            "Field",
            [
                new DetailsItem("Name", field.Name, "Short name"),
                new DetailsItem("Type", field.FieldType, "Declared field type"),
                new DetailsItem("Accessibility", field.Visibility.ToString(), "Access modifier"),
                new DetailsItem("Static", FormatBoolean(field.IsStatic), "Static member"),
                new DetailsItem("Read-only", FormatBoolean(field.IsReadOnly), "Initialized once"),
            ],
            []);
    }

    public static DetailsViewModel FromConstructor(ClassModel type, ConstructorModel constructor)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(constructor);

        return new DetailsViewModel(
            $"{type.Name}{constructor.Signature}",
            type.FullName,
            "Constructor",
            [
                new DetailsItem("Signature", constructor.Signature, "Parameters"),
                new DetailsItem("Accessibility", constructor.Visibility.ToString(), "Access modifier"),
                new DetailsItem("Parameters", Describe(constructor.Parameters.Count), "Parameter count"),
            ],
            [
                new DetailsSection(
                    "Parameters",
                    constructor.Parameters
                        .Select(parameter => new DetailsItem(
                            parameter.Name,
                            parameter.ParameterType,
                            parameter.IsOptional ? "optional" : "required"))
                        .ToList()),
            ]);
    }

    public static DetailsViewModel FromParameter(ClassModel type, ParameterModel parameter, string ownerName)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(parameter);

        return new DetailsViewModel(
            parameter.Name,
            $"{type.FullName}.{ownerName}",
            "Parameter",
            [
                new DetailsItem("Name", parameter.Name, "Parameter name"),
                new DetailsItem("Type", parameter.ParameterType, "Declared type"),
                new DetailsItem("Optional", FormatBoolean(parameter.IsOptional), "Default value allowed"),
            ],
            []);
    }

    public static DetailsViewModel FromEnumMember(ClassModel type, EnumMemberModel member)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(member);

        return new DetailsViewModel(
            member.Name,
            $"{type.FullName}.{member.Name}",
            "Enum Value",
            [
                new DetailsItem("Name", member.Name, "Member name"),
                new DetailsItem("Value", member.Value ?? "?", "Underlying value"),
                new DetailsItem("Enum", type.Name, "Declaring enum"),
            ],
            []);
    }

    private static string DescribeKind(TypeKind kind) => kind switch
    {
        TypeKind.Interface => "Interface",
        TypeKind.Enum => "Enum",
        TypeKind.Struct => "Struct",
        _ => "Class",
    };

    private static string DescribeAccessors(PropertyModel property) => (property.CanRead, property.CanWrite) switch
    {
        (true, true) => "get; set;",
        (true, false) => "get;",
        (false, true) => "set;",
        _ => "none",
    };

    private static string DescribeFlags(params bool[] flags)
    {
        var names = new List<string>
        {
            flags.Length > 0 && flags[0] ? "static" : "instance",
        };

        if (flags.Length > 1 && flags[1])
        {
            names.Add("readonly");
        }

        return string.Join(", ", names);
    }

    private static string FormatBoolean(bool value) => value ? "Yes" : "No";

    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp == DateTimeOffset.MinValue
            ? "-"
            : timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static string Describe(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Pluralize(int count, string singular, string? plural = null) =>
        count == 1 ? $"1 {singular}" : $"{Describe(count)} {plural ?? singular + "s"}";
}
