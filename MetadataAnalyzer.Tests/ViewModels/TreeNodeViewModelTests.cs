using MetadataAnalyzer.Core.Models;
using MetadataAnalyzer.WPF.ViewModels;

namespace MetadataAnalyzer.Tests.ViewModels;

public sealed class TreeNodeViewModelTests
{
    [Fact]
    public void FromAssemblyBuildsFullHierarchy()
    {
        var assembly = new MockAssembly().Model;

        var root = TreeNodeViewModel.FromAssembly(assembly);

        Assert.Equal(TreeNodeKind.Assembly, root.Kind);
        Assert.Equal("SampleLibrary", root.Label);
        Assert.Equal(2, root.Children.Count);

        var models = root.Children[0];
        Assert.Equal(TreeNodeKind.Namespace, models.Kind);
        Assert.Equal("SampleLibrary.Models", models.Label);
        Assert.Equal(2, models.Children.Count);

        var user = models.Children[0];
        Assert.Equal(TreeNodeKind.Class, user.Kind);
        Assert.Equal(5, user.Children.Count);
        Assert.Contains(user.Children, node => node.Kind == TreeNodeKind.Property);
        Assert.Contains(user.Children, node => node.Kind == TreeNodeKind.Method);
        Assert.Contains(user.Children, node => node.Kind == TreeNodeKind.Constructor);
        Assert.Contains(user.Children, node => node.Kind == TreeNodeKind.Field);
    }

    [Fact]
    public void MethodNodeExposesParameterChildren()
    {
        var root = TreeNodeViewModel.FromAssembly(new MockAssembly().Model);
        var service = root.Children[1].Children[0];

        var getUser = service.Children.First(node => node.Label.StartsWith("GetUser", StringComparison.Ordinal));

        var parameter = Assert.Single(getUser.Children);
        Assert.Equal(TreeNodeKind.Parameter, parameter.Kind);
        Assert.Equal("id", parameter.Label);
        Assert.Equal("Parameter · int", parameter.Caption);
        Assert.Equal("GetUser", parameter.OwnerName);
    }

    [Fact]
    public void ClassNodeProducesClassDetails()
    {
        var root = TreeNodeViewModel.FromAssembly(new MockAssembly().Model);

        var details = root.Children[0].Children[0].ToDetails();

        Assert.Equal("Class", details.Kind);
        Assert.Equal("User", details.Title);
        Assert.Equal("SampleLibrary.Models.User", details.Subtitle);
        Assert.Equal(4, details.Sections.Count);
    }

    [Fact]
    public void ParameterNodeProducesParameterDetails()
    {
        var root = TreeNodeViewModel.FromAssembly(new MockAssembly().Model);
        var parameter = root.Children[1].Children[0].Children
            .First(node => node.Kind == TreeNodeKind.Method)
            .Children[0];

        var details = parameter.ToDetails();

        Assert.Equal("Parameter", details.Kind);
        Assert.Equal("id", details.Title);
    }

    [Fact]
    public void AssemblyNodeProducesAssemblyDetails()
    {
        var root = TreeNodeViewModel.FromAssembly(new MockAssembly().Model);

        var details = root.ToDetails();

        Assert.Equal("Assembly", details.Kind);
        Assert.Equal("SampleLibrary", details.Title);
        Assert.Contains(details.Properties, item => item.Name == "Namespaces" && item.Type == "2");
    }

    [Fact]
    public void ChildrenAreNotifiedWhenExpansionChanges()
    {
        var root = TreeNodeViewModel.FromAssembly(new MockAssembly().Model);
        var notifications = new List<string?>();
        root.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        root.IsExpanded = false;
        root.IsSelected = true;

        Assert.Contains(nameof(TreeNodeViewModel.IsExpanded), notifications);
        Assert.Contains(nameof(TreeNodeViewModel.IsSelected), notifications);
    }

    private sealed class MockAssembly
    {
        public AssemblyModel Model { get; } = Build();

        private static AssemblyModel Build()
        {
            var user = new ClassModel(
                "User",
                "SampleLibrary.Models.User",
                Properties: [new PropertyModel("Id", "int"), new PropertyModel("Name", "string")],
                Fields: [new FieldModel("_state", "object")],
                Methods: [new MethodModel("GetFullName", "string")],
                Constructors: [new ConstructorModel()]);

            var service = new ClassModel(
                "UserService",
                "SampleLibrary.Services.UserService",
                Methods: [new MethodModel("GetUser", "User", Parameters: [new ParameterModel("id", "int")])]);

            return new AssemblyModel(
                "SampleLibrary",
                "1.0.0.0",
                "SampleLibrary.dll",
                [
                    new NamespaceModel("SampleLibrary.Models", [user, user]),
                    new NamespaceModel("SampleLibrary.Services", [service]),
                ],
                new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        }
    }
}
