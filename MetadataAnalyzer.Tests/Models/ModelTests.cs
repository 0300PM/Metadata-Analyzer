using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Tests.Models;

public sealed class ModelTests
{
    [Fact]
    public void EmptyAssemblyHasNoNamespacesOrClasses()
    {
        var assembly = AssemblyModel.Empty();

        Assert.Empty(assembly.Namespaces);
        Assert.Equal(0, assembly.NamespaceCount);
        Assert.Equal(0, assembly.ClassCount);
        Assert.Equal(0, assembly.MemberCount);
    }

    [Fact]
    public void AssemblyAggregatesCountsAcrossNamespaces()
    {
        var assembly = BuildAssembly();

        Assert.Equal(2, assembly.NamespaceCount);
        Assert.Equal(2, assembly.ClassCount);
        Assert.Equal(5, assembly.MemberCount);
        Assert.Equal(2, assembly.Classes.Count());
    }

    [Fact]
    public void ClassExposesMembersAndMemberCount()
    {
        var type = new ClassModel(
            "User",
            "SampleLibrary.Models.User",
            Methods: [new MethodModel("GetFullName", "string")],
            Properties: [new PropertyModel("Id", "int"), new PropertyModel("Name", "string")],
            Fields: [new FieldModel("_cache", "object")],
            Constructors: [new ConstructorModel()]);

        Assert.Equal(5, type.MemberCount);
        Assert.Equal("GetFullName", type.Methods[0].Name);
        Assert.Equal("object", type.BaseType ?? "object");
    }

    [Fact]
    public void MethodSignatureRendersParameters()
    {
        var method = new MethodModel(
            "GetUser",
            "User",
            Parameters:
            [
                new ParameterModel("id", "int"),
                new ParameterModel("fallback", "User", IsOptional: true),
            ]);

        Assert.Equal("GetUser(int id, User fallback = default)", method.Signature);
    }

    [Fact]
    public void ConstructorSignatureRendersParameters()
    {
        var constructor = new ConstructorModel(
            MemberVisibility.Internal,
            [new ParameterModel("repository", "IUserRepository")]);

        Assert.Equal("(IUserRepository repository)", constructor.Signature);
        Assert.Equal(MemberVisibility.Internal, constructor.Visibility);
    }

    [Fact]
    public void CollectionsDefaultToEmptyNotNullableLists()
    {
        var assembly = new AssemblyModel("Sample", "1.0.0.0", "Sample.dll");

        Assert.NotNull(assembly.Namespaces);
        Assert.NotNull(new NamespaceModel("Sample").Classes);
        Assert.NotNull(new ClassModel("A", "A").Methods);
        Assert.NotNull(new MethodModel("M", "void").Parameters);
    }

    [Fact]
    public void ModelsSupportValueEquality()
    {
        var left = new PropertyModel("Id", "int");
        var right = new PropertyModel("Id", "int");

        Assert.Equal(left, right);
    }

    internal static AssemblyModel BuildAssembly()
    {
        var user = new ClassModel(
            "User",
            "SampleLibrary.Models.User",
            Properties: [new PropertyModel("Id", "int"), new PropertyModel("Name", "string")],
            Methods: [new MethodModel("GetFullName", "string")],
            Constructors: [new ConstructorModel()]);

        var logger = new ClassModel(
            "Logger",
            "SampleLibrary.Helpers.Logger",
            Methods: [new MethodModel("Write", "void", IsStatic: true)]);

        return new AssemblyModel(
            "SampleLibrary",
            "1.0.0.0",
            "SampleLibrary.dll",
            [
                new NamespaceModel("SampleLibrary.Models", [user]),
                new NamespaceModel("SampleLibrary.Helpers", [logger]),
            ]);
    }
}
