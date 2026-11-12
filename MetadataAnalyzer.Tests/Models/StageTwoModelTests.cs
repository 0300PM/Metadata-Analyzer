using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.Tests.Models;

public sealed class StageTwoModelTests
{
    [Fact]
    public void ParameterRendersModifierAndDefaultValue()
    {
        var optional = new ParameterModel("email", "string", IsOptional: true, Position: 1, DefaultValue: "\"none\"");
        var reference = new ParameterModel("value", "int", IsReference: true);

        Assert.Equal("string email = \"none\"", optional.DisplayName);
        Assert.Equal("ref int value", reference.DisplayName);
        Assert.Equal("out int value", new ParameterModel("value", "int", IsOut: true).DisplayName);
        Assert.Equal(1, optional.Position);
    }

    [Fact]
    public void MethodModifierDescribesSignatureFlags()
    {
        Assert.Equal("static", new MethodModel("Run", "void", IsStatic: true).Modifier);
        Assert.Equal("abstract", new MethodModel("Run", "void", IsAbstract: true).Modifier);
        Assert.Equal("virtual", new MethodModel("Run", "void", IsVirtual: true).Modifier);
        Assert.Equal("override", new MethodModel("Run", "void", IsOverride: true).Modifier);
        Assert.Equal("extension", new MethodModel("Run", "void", IsExtension: true).Modifier);
        Assert.Equal("instance", new MethodModel("Run", "void").Modifier);
    }

    [Fact]
    public void PropertyAccessorsFollowReadWriteFlags()
    {
        Assert.Equal("get; set;", new PropertyModel("Name", "string").Accessors);
        Assert.Equal("get;", new PropertyModel("Name", "string", CanWrite: false).Accessors);
        Assert.Equal("set;", new PropertyModel("Name", "string", CanRead: false).Accessors);
        Assert.Equal("none", new PropertyModel("Name", "string", CanRead: false, CanWrite: false).Accessors);
        Assert.True(new PropertyModel("Item", "int", HasIndexer: true).HasIndexer);
    }

    [Fact]
    public void FieldModifierDescribesConstantAndReadOnly()
    {
        Assert.Equal("const", new FieldModel("Max", "int", IsConstant: true).Modifier);
        Assert.Equal("static readonly", new FieldModel("Max", "int", IsStatic: true, IsReadOnly: true).Modifier);
        Assert.Equal("readonly", new FieldModel("Max", "int", IsReadOnly: true).Modifier);
        Assert.Equal("instance", new FieldModel("Max", "int").Modifier);
    }

    [Fact]
    public void ConstructorExposesStaticFlag()
    {
        var constructor = new ConstructorModel(MemberVisibility.Private, [], IsStatic: true);

        Assert.Equal("static ", constructor.Modifier);
        Assert.Equal("()", constructor.Signature);
    }

    [Fact]
    public void TypeModelExposesInterfacesAndDelegateKind()
    {
        var handler = new ClassModel(
            "Handler",
            "Sample.Handler",
            TypeKind: TypeKind.Delegate,
            Interfaces: ["IAsyncResult"],
            IsSealed: true,
            IsGeneric: false,
            AssemblyName: "Sample");

        Assert.True(handler.ImplementsInterfaces);
        Assert.Equal(TypeKind.Delegate, handler.TypeKind);
        Assert.True(handler.IsSealed);
        Assert.Equal("Sample", handler.AssemblyName);
        Assert.False(new ClassModel("Empty", "Empty").ImplementsInterfaces);
    }

    [Fact]
    public void AssemblyModelKeepsLocationAndFilePath()
    {
        var assembly = new AssemblyModel(
            "Sample",
            "1.0.0.0",
            "Sample.dll",
            FilePath: @"C:\libs\Sample.dll",
            Location: AssemblyLocation.File);

        Assert.True(assembly.HasFile);
        Assert.True(assembly.IsLoaded);
        Assert.Equal(AssemblyLocation.File, assembly.Location);
        Assert.False(AssemblyModel.Empty().HasFile);
        Assert.False(AssemblyModel.Empty().IsLoaded);
    }
}
