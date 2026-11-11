using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.WPF.ViewModels;

public sealed class TreeNodeViewModel : ObservableObject
{
    private bool _isExpanded;
    private bool _isSelected;

    private TreeNodeViewModel(
        string label,
        string caption,
        TreeNodeKind kind,
        object model,
        ClassModel? owner = null,
        string? ownerName = null)
    {
        Label = label;
        Caption = caption;
        Kind = kind;
        Model = model;
        OwnerClass = owner;
        OwnerName = ownerName;
    }

    public TreeNodeViewModel? Parent { get; private set; }

    public string Label { get; }

    public string Caption { get; }

    public TreeNodeKind Kind { get; }

    public object Model { get; }

    public ClassModel? OwnerClass { get; }

    public string? OwnerName { get; }

    public ClassModel? Owner => Model as ClassModel;

    public ObservableCollection<TreeNodeViewModel> Children { get; } = [];

    private void Add(TreeNodeViewModel child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    public bool HasChildren => Children.Count > 0;

    public string Tooltip => string.IsNullOrEmpty(Caption) ? Label : $"{Label} · {Caption}";

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public static TreeNodeViewModel FromAssembly(AssemblyModel assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var node = new TreeNodeViewModel(assembly.Name, "Assembly", TreeNodeKind.Assembly, assembly)
        {
            IsExpanded = true,
        };

        foreach (var @namespace in assembly.Namespaces)
        {
            node.Add(FromNamespace(@namespace));
        }

        return node;
    }

    public DetailsViewModel ToDetails() => Kind switch
    {
        TreeNodeKind.Assembly when Model is AssemblyModel assembly =>
            DetailsViewModel.FromAssembly(assembly),
        TreeNodeKind.Namespace when Model is NamespaceModel @namespace =>
            DetailsViewModel.FromNamespace(@namespace),
        TreeNodeKind.Class or TreeNodeKind.Interface or TreeNodeKind.Enum or TreeNodeKind.Struct
            when Model is ClassModel type => DetailsViewModel.FromClass(type),
        TreeNodeKind.Method when Model is MethodModel method && OwnerClass is not null =>
            DetailsViewModel.FromMethod(OwnerClass, method),
        TreeNodeKind.Property when Model is PropertyModel property && OwnerClass is not null =>
            DetailsViewModel.FromProperty(OwnerClass, property),
        TreeNodeKind.Field when Model is FieldModel field && OwnerClass is not null =>
            DetailsViewModel.FromField(OwnerClass, field),
        TreeNodeKind.Constructor when Model is ConstructorModel constructor && OwnerClass is not null =>
            DetailsViewModel.FromConstructor(OwnerClass, constructor),
        TreeNodeKind.Parameter when Model is ParameterModel parameter && OwnerClass is not null =>
            DetailsViewModel.FromParameter(OwnerClass, parameter, OwnerName ?? string.Empty),
        TreeNodeKind.Value when Model is EnumMemberModel value && OwnerClass is not null =>
            DetailsViewModel.FromEnumMember(OwnerClass, value),
        _ => DetailsViewModel.Empty,
    };

    public IEnumerable<TreeNodeViewModel> Flatten()
    {
        yield return this;

        foreach (var child in Children)
        {
            foreach (var descendant in child.Flatten())
            {
                yield return descendant;
            }
        }
    }

    public void SetExpandedRecursively(bool expanded)
    {
        foreach (var node in Flatten())
        {
            node.IsExpanded = expanded;
        }
    }

    private static TreeNodeViewModel FromNamespace(NamespaceModel @namespace)
    {
        var node = new TreeNodeViewModel(@namespace.Name, "Namespace", TreeNodeKind.Namespace, @namespace);

        foreach (var type in @namespace.Classes)
        {
            node.Add(FromType(type));
        }

        return node;
    }

    private static TreeNodeViewModel FromType(ClassModel type)
    {
        var node = new TreeNodeViewModel(type.Name, DescribeKind(type.TypeKind), MapKind(type.TypeKind), type);

        foreach (var enumMember in type.EnumMembers)
        {
            node.Add(new TreeNodeViewModel(
                enumMember.Name,
                $"Value = {enumMember.Value ?? "?"}",
                TreeNodeKind.Value,
                enumMember,
                type));
        }

        foreach (var field in type.Fields)
        {
            node.Add(new TreeNodeViewModel(field.Name, field.FieldType, TreeNodeKind.Field, field, type));
        }

        foreach (var property in type.Properties)
        {
            node.Add(new TreeNodeViewModel(
                property.Name,
                property.PropertyType,
                TreeNodeKind.Property,
                property,
                type));
        }

        foreach (var constructor in type.Constructors)
        {
            node.Add(new TreeNodeViewModel(
                constructor.Signature,
                "Constructor",
                TreeNodeKind.Constructor,
                constructor,
                type));
        }

        foreach (var method in type.Methods)
        {
            node.Add(FromMethod(type, method));
        }

        return node;
    }

    private static TreeNodeViewModel FromMethod(ClassModel type, MethodModel method)
    {
        var node = new TreeNodeViewModel(
            $"{method.Name}({string.Join(", ", method.Parameters.Select(parameter => parameter.ParameterType))})",
            method.ReturnType,
            TreeNodeKind.Method,
            method,
            type);

        foreach (var parameter in method.Parameters)
        {
            node.Add(new TreeNodeViewModel(
                parameter.Name,
                $"Parameter · {parameter.ParameterType}",
                TreeNodeKind.Parameter,
                parameter,
                type,
                method.Name));
        }

        return node;
    }

    private static TreeNodeKind MapKind(TypeKind kind) => kind switch
    {
        TypeKind.Interface => TreeNodeKind.Interface,
        TypeKind.Enum => TreeNodeKind.Enum,
        TypeKind.Struct => TreeNodeKind.Struct,
        _ => TreeNodeKind.Class,
    };

    private static string DescribeKind(TypeKind kind) => kind switch
    {
        TypeKind.Interface => "Interface",
        TypeKind.Enum => "Enum",
        TypeKind.Struct => "Struct",
        _ => "Class",
    };
}
