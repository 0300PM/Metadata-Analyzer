using System.Globalization;
using System.Text;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Core.Services;

public sealed class MetadataFormatter : IMetadataFormatter
{
    private const string VerticalBar = "|";
    private const string TreeIndent = "    ";
    private const string BranchConnector = "|   +-- ";
    private const string LastConnector = "+-- ";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public string FormatTree(AssemblyModel assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var builder = new StringBuilder();
        AppendLine(builder, "Assembly: {0}", assembly.Name);
        AppendLine(builder, "Version: {0}", assembly.Version);
        builder.AppendLine();

        AppendChildren(builder, RenderNamespaces(assembly), 0);

        return builder.ToString().TrimEnd();
    }

    public string FormatDetails(AssemblyModel assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var builder = new StringBuilder();
        AppendLine(builder, "Assembly:     {0}", assembly.Name);
        AppendLine(builder, "Version:      {0}", assembly.Version);
        AppendLine(builder, "File name:    {0}", Describe(assembly.FileName));
        AppendLine(builder, "Namespaces:   {0}", assembly.NamespaceCount);
        AppendLine(builder, "Classes:      {0}", assembly.ClassCount);
        AppendLine(builder, "Members:      {0}", assembly.MemberCount);
        AppendLine(builder, "Loaded at:    {0}", FormatTimestamp(assembly.LoadedAt));
        builder.AppendLine();

        if (assembly.Namespaces.Count == 0)
        {
            AppendLine(builder, "Assembly contains no namespaces.");
            return builder.ToString().TrimEnd();
        }

        AppendLine(builder, "Namespaces:");
        foreach (var @namespace in assembly.Namespaces)
        {
            AppendLine(
                builder,
                "  {0}  ({1})",
                Describe(@namespace.Name),
                Pluralize(@namespace.Classes.Count, "class", "classes"));

            foreach (var @class in @namespace.Classes)
            {
                AppendLine(builder, "    {0}  ({1})", Describe(@class.Name), Pluralize(@class.MemberCount, "member"));
            }
        }

        return builder.ToString().TrimEnd();
    }

    public string FormatMemberDetails(ClassModel type, string memberName)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(memberName);

        var builder = new StringBuilder();
        AppendLine(builder, "Class:        {0}", type.FullName);

        if (!TryDescribeMember(builder, type, memberName))
        {
            AppendLine(builder, "Member:       {0} (not found)", memberName);
        }

        return builder.ToString().TrimEnd();
    }

    private static bool TryDescribeMember(StringBuilder builder, ClassModel type, string memberName)
    {
        var property = type.Properties.FirstOrDefault(item => item.Name == memberName);
        if (property is not null)
        {
            AppendLine(builder, "Kind:         Property");
            AppendLine(builder, "Type:         {0}", property.PropertyType);
            AppendLine(builder, "Visibility:   {0}", property.Visibility);
            AppendLine(builder, "Accessors:    {0}", DescribeAccessors(property));
            return true;
        }

        var method = type.Methods.FirstOrDefault(item => item.Name == memberName);
        if (method is not null)
        {
            AppendLine(builder, "Kind:         Method");
            AppendLine(builder, "Signature:    {0}", method.Signature);
            AppendLine(builder, "Return type:  {0}", method.ReturnType);
            AppendLine(builder, "Visibility:   {0}", method.Visibility);
            AppendLine(builder, "Static:       {0}", FormatBoolean(method.IsStatic));
            AppendParameters(builder, method.Parameters);
            return true;
        }

        var field = type.Fields.FirstOrDefault(item => item.Name == memberName);
        if (field is not null)
        {
            AppendLine(builder, "Kind:         Field");
            AppendLine(builder, "Type:         {0}", field.FieldType);
            AppendLine(builder, "Visibility:   {0}", field.Visibility);
            AppendLine(builder, "Static:       {0}", FormatBoolean(field.IsStatic));
            AppendLine(builder, "Read-only:    {0}", FormatBoolean(field.IsReadOnly));
            return true;
        }

        var constructor = type.Constructors.FirstOrDefault(item => DescribeConstructor(item) == memberName);
        if (constructor is not null)
        {
            AppendLine(builder, "Kind:         Constructor");
            AppendLine(builder, "Signature:    {0}", DescribeConstructor(constructor));
            AppendLine(builder, "Visibility:   {0}", constructor.Visibility);
            AppendParameters(builder, constructor.Parameters);
            return true;
        }

        return false;
    }

    private static void AppendParameters(StringBuilder builder, IReadOnlyList<ParameterModel> parameters)
    {
        AppendLine(builder, "Parameters:   {0}", parameters.Count == 0 ? "none" : parameters.Count.ToString(Invariant));

        foreach (var parameter in parameters)
        {
            AppendLine(builder, "  - {0}", parameter.DisplayName);
        }
    }

    private static List<TreeNode> RenderNamespaces(AssemblyModel assembly)
    {
        var namespaceNodes = new List<TreeNode>(assembly.Namespaces.Count);

        foreach (var @namespace in assembly.Namespaces)
        {
            var classNodes = new List<TreeNode>(@namespace.Classes.Count);

            foreach (var @class in @namespace.Classes)
            {
                classNodes.Add(new TreeNode($"{DescribeKind(@class.TypeKind)}: {Describe(@class.Name)}", RenderMembers(@class)));
            }

            namespaceNodes.Add(new TreeNode($"Namespace: {Describe(@namespace.Name)}", classNodes));
        }

        return namespaceNodes;
    }

    private static List<TreeNode> RenderMembers(ClassModel type)
    {
        var nodes = new List<TreeNode>();

        nodes.AddRange(type.EnumMembers.Select(member => new TreeNode($"Value: {member.Name} = {member.Value ?? "?"}")));

        nodes.AddRange(type.Fields.Select(field => new TreeNode($"Field: {field.Name} ({field.FieldType})")));
        nodes.AddRange(type.Properties.Select(property => new TreeNode($"Property: {property.Name} ({property.PropertyType})")));
        nodes.AddRange(type.Constructors.Select(constructor => new TreeNode($"Constructor: {DescribeConstructor(constructor)}")));
        nodes.AddRange(type.Methods.Select(method => new TreeNode($"Method: {method.Signature} : {method.ReturnType}")));

        return nodes;
    }

    private static void AppendChildren(StringBuilder builder, IReadOnlyList<TreeNode> nodes, int depth)
    {
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            var isLast = index == nodes.Count - 1;

            builder.Append(GetIndent(depth)).Append(isLast ? LastConnector : BranchConnector).AppendLine(node.Label);

            if (node.Children.Count > 0)
            {
                AppendChildren(builder, node.Children, depth + 1);
            }
        }
    }

    private static string GetIndent(int depth)
    {
        var builder = new StringBuilder(depth * TreeIndent.Length);

        for (var level = 0; level < depth; level++)
        {
            builder.Append(TreeIndent);
        }

        return builder.ToString();
    }

    private static void AppendLine(StringBuilder builder, string format, params object[] arguments) =>
        builder.AppendLine(string.Format(Invariant, format, arguments));

    private static void AppendLine(StringBuilder builder, string value) => builder.AppendLine(value);

    private static string Describe(string value) => string.IsNullOrWhiteSpace(value) ? "(unknown)" : value;

    private static string DescribeKind(TypeKind kind) => kind switch
    {
        TypeKind.Interface => "Interface",
        TypeKind.Enum => "Enum",
        TypeKind.Struct => "Struct",
        _ => "Class",
    };

    private static string DescribeConstructor(ConstructorModel constructor) =>
        $"{constructor.Visibility.ToString().ToLowerInvariant()} {constructor.Signature}";

    private static string DescribeAccessors(PropertyModel property) => (property.CanRead, property.CanWrite) switch
    {
        (true, true) => "get, set",
        (true, false) => "get",
        (false, true) => "set",
        _ => "none",
    };

    private static string FormatBoolean(bool value) => value ? "yes" : "no";

    private static string FormatTimestamp(DateTimeOffset timestamp) =>
        timestamp == DateTimeOffset.MinValue
            ? "(not loaded)"
            : timestamp.ToString("yyyy-MM-dd HH:mm:ss", Invariant);

    private static string Pluralize(int count, string singular, string? plural = null) =>
        count == 1 ? $"{count} {singular}" : $"{count} {plural ?? singular + "s"}";

    private sealed record TreeNode(string Label, IReadOnlyList<TreeNode>? Children = null)
    {
        public IReadOnlyList<TreeNode> Children { get; } = Children ?? [];
    }
}
