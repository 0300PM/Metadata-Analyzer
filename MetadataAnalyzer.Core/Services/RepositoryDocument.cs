using System.Xml;
using System.Xml.Serialization;
using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Services;

[XmlRoot(RepositoryDocument.RootName)]
public sealed class RepositoryDocument
{
    public const string RootName = "metadataAnalysis";

    public const string SchemaVersion = "1.0";

    [XmlAttribute("schemaVersion")]
    public string SchemaVersionValue { get; set; } = SchemaVersion;

    [XmlAttribute("savedAt")]
    public string SavedAt { get; set; } = string.Empty;

    public AssemblyElement Assembly { get; set; } = new();
}

public sealed class AssemblyElement
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("version")]
    public string Version { get; set; } = string.Empty;

    [XmlAttribute("culture")]
    public string Culture { get; set; } = "neutral";

    [XmlAttribute("publicKeyToken")]
    public string PublicKeyToken { get; set; } = string.Empty;

    [XmlAttribute("fullName")]
    public string? FullName { get; set; }

    [XmlAttribute("fileName")]
    public string FileName { get; set; } = string.Empty;

    [XmlAttribute("location")]
    public AssemblyLocation Location { get; set; }

    [XmlAttribute("loadedAt")]
    public string LoadedAt { get; set; } = string.Empty;

    [XmlAttribute("runtimeVersion")]
    public string? RuntimeVersion { get; set; }

    [XmlElement("filePath")]
    public string? FilePath { get; set; }

    [XmlArray("namespaces")]
    [XmlArrayItem("namespace")]
    public List<NamespaceElement> Namespaces { get; set; } = [];
}

public sealed class NamespaceElement
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlArray("types")]
    [XmlArrayItem("type")]
    public List<TypeElement> Types { get; set; } = [];
}

public sealed class TypeElement
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("fullName")]
    public string FullName { get; set; } = string.Empty;

    [XmlAttribute("kind")]
    public TypeKind Kind { get; set; }

    [XmlAttribute("visibility")]
    public MemberVisibility Visibility { get; set; }

    [XmlAttribute("baseType")]
    public string? BaseType { get; set; }

    [XmlAttribute("assemblyName")]
    public string? AssemblyName { get; set; }

    [XmlAttribute("declaringType")]
    public string? DeclaringType { get; set; }

    [XmlAttribute("isStatic")]
    public bool IsStatic { get; set; }

    [XmlAttribute("isAbstract")]
    public bool IsAbstract { get; set; }

    [XmlAttribute("isSealed")]
    public bool IsSealed { get; set; }

    [XmlAttribute("isGeneric")]
    public bool IsGeneric { get; set; }

    [XmlAttribute("isNested")]
    public bool IsNested { get; set; }

    [XmlAttribute("isFlagsEnum")]
    public bool IsFlagsEnum { get; set; }

    [XmlArray("genericParameters")]
    [XmlArrayItem("parameter")]
    public List<string> GenericParameters { get; set; } = [];

    [XmlArray("interfaces")]
    [XmlArrayItem("interface")]
    public List<string> Interfaces { get; set; } = [];

    [XmlArray("enumValues")]
    [XmlArrayItem("value")]
    public List<EnumMemberElement> EnumMembers { get; set; } = [];

    [XmlArray("fields")]
    [XmlArrayItem("field")]
    public List<FieldElement> Fields { get; set; } = [];

    [XmlArray("properties")]
    [XmlArrayItem("property")]
    public List<PropertyElement> Properties { get; set; } = [];

    [XmlArray("constructors")]
    [XmlArrayItem("constructor")]
    public List<ConstructorElement> Constructors { get; set; } = [];

    [XmlArray("methods")]
    [XmlArrayItem("method")]
    public List<MethodElement> Methods { get; set; } = [];
}

public sealed class EnumMemberElement
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("value")]
    public string? Value { get; set; }

    [XmlAttribute("numericValue")]
    public string? NumericValue { get; set; }
}

public sealed class FieldElement
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("type")]
    public string Type { get; set; } = string.Empty;

    [XmlAttribute("visibility")]
    public MemberVisibility Visibility { get; set; }

    [XmlAttribute("isStatic")]
    public bool IsStatic { get; set; }

    [XmlAttribute("isReadOnly")]
    public bool IsReadOnly { get; set; }

    [XmlAttribute("isConstant")]
    public bool IsConstant { get; set; }
}

public sealed class PropertyElement
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("type")]
    public string Type { get; set; } = string.Empty;

    [XmlAttribute("visibility")]
    public MemberVisibility Visibility { get; set; }

    [XmlAttribute("setterVisibility")]
    public string? SetterVisibility { get; set; }

    [XmlAttribute("canRead")]
    public bool CanRead { get; set; }

    [XmlAttribute("canWrite")]
    public bool CanWrite { get; set; }

    [XmlAttribute("isStatic")]
    public bool IsStatic { get; set; }

    [XmlAttribute("hasIndexer")]
    public bool HasIndexer { get; set; }
}

public sealed class ConstructorElement
{
    [XmlAttribute("visibility")]
    public MemberVisibility Visibility { get; set; }

    [XmlAttribute("isStatic")]
    public bool IsStatic { get; set; }

    [XmlArray("parameters")]
    [XmlArrayItem("parameter")]
    public List<ParameterElement> Parameters { get; set; } = [];
}

public sealed class MethodElement
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("returnType")]
    public string ReturnType { get; set; } = string.Empty;

    [XmlAttribute("visibility")]
    public MemberVisibility Visibility { get; set; }

    [XmlAttribute("isStatic")]
    public bool IsStatic { get; set; }

    [XmlAttribute("isAbstract")]
    public bool IsAbstract { get; set; }

    [XmlAttribute("isVirtual")]
    public bool IsVirtual { get; set; }

    [XmlAttribute("isOverride")]
    public bool IsOverride { get; set; }

    [XmlAttribute("isExtension")]
    public bool IsExtension { get; set; }

    [XmlArray("genericParameters")]
    [XmlArrayItem("parameter")]
    public List<string> GenericParameters { get; set; } = [];

    [XmlArray("parameters")]
    [XmlArrayItem("parameter")]
    public List<ParameterElement> Parameters { get; set; } = [];
}

public sealed class ParameterElement
{
    [XmlAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [XmlAttribute("type")]
    public string Type { get; set; } = string.Empty;

    [XmlAttribute("position")]
    public int Position { get; set; }

    [XmlAttribute("isOptional")]
    public bool IsOptional { get; set; }

    [XmlAttribute("isOut")]
    public bool IsOut { get; set; }

    [XmlAttribute("isIn")]
    public bool IsIn { get; set; }

    [XmlAttribute("isReference")]
    public bool IsReference { get; set; }

    [XmlAttribute("defaultValue")]
    public string? DefaultValue { get; set; }
}
