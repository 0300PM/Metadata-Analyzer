using System.Globalization;
using System.IO;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;
using MetadataAnalyzer.Core.Services;
using MetadataAnalyzer.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace MetadataAnalyzer.Tests.Services;

public sealed class ReflectionMetadataProviderTests
{
    private static readonly string FixturePath = Path.Combine(AppContext.BaseDirectory, "SampleAssembly.dll");

    private readonly ReflectionMetadataProvider _provider = new(
        new FakeClock(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task LoadAsyncReadsAssemblyIdentity()
    {
        var assembly = await LoadFixtureAsync();

        Assert.Equal("SampleAssembly", assembly.Name);
        Assert.Equal("1.0.0.0", assembly.Version);
        Assert.Equal("SampleAssembly.dll", assembly.FileName);
        Assert.Equal(FixturePath, assembly.FilePath);
        Assert.Equal(AssemblyLocation.File, assembly.Location);
        Assert.False(string.IsNullOrWhiteSpace(assembly.FullName));
        Assert.Equal("neutral", assembly.Culture);
        Assert.False(assembly.IsStrongNamed);
        Assert.False(string.IsNullOrWhiteSpace(assembly.RuntimeVersion));
        Assert.Equal(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero), assembly.LoadedAt);
    }

    [Fact]
    public async Task LoadAsyncGroupsTypesByNamespace()
    {
        var assembly = await LoadFixtureAsync();

        var @namespace = Assert.Single(assembly.Namespaces);
        Assert.Equal("MetadataAnalyzer.Fixtures", @namespace.Name);
        Assert.Contains(@namespace.Classes, type => type.Name == "SampleService");
    }

    [Theory]
    [InlineData("SampleService", TypeKind.Class)]
    [InlineData("SampleBase`1", TypeKind.Class)]
    [InlineData("ISampleContract", TypeKind.Interface)]
    [InlineData("SamplePoint", TypeKind.Struct)]
    [InlineData("SampleState", TypeKind.Enum)]
    [InlineData("SampleHandler", TypeKind.Delegate)]
    public async Task LoadAsyncRecognisesTypeKind(string name, TypeKind expected)
    {
        var type = Find(await LoadFixtureAsync(), name);

        Assert.Equal(expected, type.TypeKind);
    }

    [Fact]
    public async Task LoadAsyncReadsBaseTypeInterfacesAndFlags()
    {
        var assembly = await LoadFixtureAsync();

        var service = Find(assembly, "SampleService");
        Assert.Equal("SampleBase<Int32>", service.BaseType);
        Assert.Contains("ISampleContract", service.Interfaces);
        Assert.True(service.ImplementsInterfaces);
        Assert.True(service.IsSealed);
        Assert.Equal(MemberVisibility.Public, service.Visibility);

        var contract = Find(assembly, "ISampleContract");
        Assert.True(contract.IsInterface);
        Assert.True(contract.IsAbstract);
        Assert.Null(contract.BaseType);

        var options = Find(assembly, "SampleOptions");
        Assert.True(options.IsFlagsEnum);
        Assert.False(Find(assembly, "SampleState").IsFlagsEnum);
    }

    [Fact]
    public async Task LoadAsyncReadsGenericParameters()
    {
        var assembly = await LoadFixtureAsync();

        var baseType = Find(assembly, "SampleBase`1");
        Assert.True(baseType.IsGeneric);
        Assert.Equal(["T"], baseType.GenericParameters);
        Assert.Equal("<T>", baseType.GenericSuffix);

        var map = Assert.Single(Find(assembly, "SampleService").Methods, method => method.Name == "Map");
        Assert.True(map.IsGeneric);
        Assert.Equal(["TResult"], map.GenericParameters);
        Assert.Equal("Map<TResult>(Func<Int32, TResult> selector)", map.Signature);
    }

    [Fact]
    public async Task LoadAsyncReadsConstructorsAndModifiers()
    {
        var service = Find(await LoadFixtureAsync(), "SampleService");

        Assert.Contains(service.Constructors, constructor => constructor.IsStatic);
        Assert.Contains(service.Constructors, constructor => constructor.Visibility == MemberVisibility.Public);
        Assert.Contains(
            service.Constructors,
            constructor => constructor.Parameters.Any(parameter => parameter is { Name: "value", IsOptional: true, DefaultValue: "5" }));

        var create = Assert.Single(service.Methods, method => method.Name == "Create");
        Assert.True(create.IsStatic);
        Assert.Equal("static", create.Modifier);

        var raise = Assert.Single(service.Methods, method => method.Name == "RaiseChanged");
        Assert.Equal(MemberVisibility.Private, raise.Visibility);
    }

    [Fact]
    public async Task LoadAsyncReadsParametersWithModifiers()
    {
        var service = Find(await LoadFixtureAsync(), "SampleService");

        var configure = Assert.Single(service.Methods, method => method.Name == "Configure");
        Assert.Equal(["counter", "message", "point"], configure.Parameters.Select(parameter => parameter.Name));
        Assert.Equal("ref Int32 counter", configure.Parameters[0].DisplayName);
        Assert.Equal("out String message", configure.Parameters[1].DisplayName);
        Assert.Equal("in SamplePoint point", configure.Parameters[2].DisplayName);
    }

    [Fact]
    public async Task LoadAsyncReadsPropertiesAndFields()
    {
        var assembly = await LoadFixtureAsync();
        var service = Find(assembly, "SampleService");
        var baseType = Find(assembly, "SampleBase`1");

        Assert.Contains(baseType.Properties, property => property is { Name: "Name", CanRead: true, CanWrite: false });
        Assert.Contains(baseType.Properties, property => property is { Name: "IsActive", SetterVisibility: MemberVisibility.Private });
        Assert.Contains(service.Fields, field => field is { Name: "_value", Visibility: MemberVisibility.Private, IsStatic: false });
        Assert.Contains(service.Fields, field => field is { Name: "Sync", IsStatic: true, IsReadOnly: true });

        var payload = baseType.Properties.Single(property => property.Name == "Payload");
        Assert.Equal("T", payload.PropertyType);
    }

    [Fact]
    public async Task LoadAsyncReadsEnumMembersAndUnderlyingValues()
    {
        var assembly = await LoadFixtureAsync();

        var state = Find(assembly, "SampleState");
        Assert.Equal(
            [("None", 0L), ("Started", 1L), ("Finished", 2L)],
            state.EnumMembers.Select(member => (member.Name, member.NumericValue ?? -1)));

        var options = Find(assembly, "SampleOptions");
        Assert.All(options.EnumMembers, member => Assert.NotNull(member.Value));
    }

    [Fact]
    public async Task LoadAsyncReadsNestedTypes()
    {
        var nested = Find(await LoadFixtureAsync(), "Nested");

        Assert.True(nested.IsNested);
        Assert.Equal("MetadataAnalyzer.Fixtures.SampleService+Nested", nested.FullName);
        Assert.Contains(nested.Properties, property => property.Name == "Depth");
    }

    [Fact]
    public async Task LoadAsyncSkipsNestedTypesWhenDisabled()
    {
        var provider = new ReflectionMetadataProvider(options: new ReflectionMetadataOptions { IncludeNestedTypes = false });

        var assembly = await provider.LoadAsync(new MetadataSource(FixturePath));

        Assert.DoesNotContain(assembly.Classes, type => type.IsNested);
    }

    [Fact]
    public async Task LoadAsyncDoesNotExecuteAnalysedCode()
    {
        var assembly = await LoadFixtureAsync();

        Assert.False(
            AppDomain.CurrentDomain.GetAssemblies().Any(item => item.GetName().Name == "SampleAssembly"),
            "The analysed assembly must not be loaded into the application context.");
        Assert.NotEmpty(assembly.Classes);
    }

    [Fact]
    public async Task LoadAsyncRejectsMissingFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.dll");

        await Assert.ThrowsAsync<FileNotFoundException>(() => _provider.LoadAsync(new MetadataSource(path)));
    }

    [Fact]
    public async Task LoadAsyncRejectsInvalidImage()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("broken.dll");
        await File.WriteAllTextAsync(path, "not an assembly");

        await Assert.ThrowsAsync<InvalidDataException>(() => _provider.LoadAsync(new MetadataSource(path)));
    }

    [Fact]
    public async Task LoadAsyncRejectsMissingSource()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _provider.LoadAsync(MetadataSource.Empty));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _provider.LoadAsync());
    }

    [Fact]
    public async Task LoadAsyncHonoursCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _provider.LoadAsync(new MetadataSource(FixturePath), cancellation.Token));
    }

    [Fact]
    public async Task LoadAsyncRejectsInvalidPath()
    {
        var invalid = "C:\\libs\\invalid\0path.dll";

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _provider.LoadAsync(new MetadataSource(invalid)));

        Assert.Contains("not valid", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsyncRejectsNonAssemblyFile()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("notes.dll");
        await File.WriteAllTextAsync(path, "this is not an assembly");

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => _provider.LoadAsync(new MetadataSource(path)));

        Assert.Contains("not a valid .NET assembly", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoadAsyncWrapsUnreadableFile()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("locked.dll");
        File.Copy(FixturePath, path);

        await using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => _provider.LoadAsync(new MetadataSource(path)));

        Assert.Contains("could not be read", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public async Task LoadAsyncHandlesMissingDependencies()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("SampleAssembly.dll");
        File.Copy(FixturePath, path);

        var assembly = await new ReflectionMetadataProvider().LoadAsync(new MetadataSource(path));

        Assert.Equal("SampleAssembly", assembly.Name);
        Assert.NotEmpty(assembly.Classes);
    }

    [Fact]
    public async Task ResolverMapsLoaderFailuresToAnalysisException()
    {
        using var directory = new TemporaryDirectory();
        var path = directory.File("broken.dll");
        await File.WriteAllTextAsync(path, "not an assembly");

        var services = new ServiceCollection();
        services.AddMetadataAnalyzerCore(useMockData: false);
        using var provider = services.BuildServiceProvider();

        var resolver = new MetadataProviderResolver(
            provider,
            new MetadataProviderOptions { UseMockData = false, FilePath = path });

        await Assert.ThrowsAsync<InvalidDataException>(() => resolver.LoadAsync());
    }

    [Fact]
    public async Task LoadAsyncReportsMemberCounts()
    {
        var assembly = await LoadFixtureAsync();

        Assert.Equal(1, assembly.NamespaceCount);
        Assert.True(assembly.ClassCount >= 7);
        Assert.True(assembly.MemberCount > 20);
    }

    [Fact]
    public async Task ResolverPrefersFileSourceOverMockData()
    {
        var services = new ServiceCollection();
        services.AddMetadataAnalyzerCore(useMockData: true);
        using var provider = services.BuildServiceProvider();

        var resolver = new MetadataProviderResolver(
            provider,
            new MetadataProviderOptions { UseMockData = true, FilePath = null });

        var assembly = await resolver.LoadAsync(new MetadataSource(FixturePath));

        Assert.Equal("SampleAssembly", assembly.Name);
    }

    [Fact]
    public async Task ResolverPrefersConfiguredFileOverMockData()
    {
        var services = new ServiceCollection();
        services.AddMetadataAnalyzerCore(useMockData: true);
        using var provider = services.BuildServiceProvider();

        var resolver = new MetadataProviderResolver(
            provider,
            new MetadataProviderOptions { UseMockData = true, FilePath = FixturePath });

        var assembly = await resolver.LoadAsync();

        Assert.Equal("SampleAssembly", assembly.Name);
    }

    [Fact]
    public async Task ResolverUsesMockDataWhenNoSourceIsProvided()
    {
        var services = new ServiceCollection();
        services.AddMetadataAnalyzerCore(useMockData: true);
        using var provider = services.BuildServiceProvider();

        var resolver = new MetadataProviderResolver(
            provider,
            new MetadataProviderOptions { UseMockData = true, FilePath = null });

        var assembly = await resolver.LoadAsync();

        Assert.NotEqual("SampleAssembly", assembly.Name);
        Assert.NotEmpty(assembly.Classes);
    }

    [Fact]
    public async Task LoadAsyncKeepsNestedTypeQualifiedName()
    {
        var nested = Find(await LoadFixtureAsync(), "Nested");

        Assert.Equal("MetadataAnalyzer.Fixtures.SampleService+Nested", nested.QualifiedName);
        Assert.Equal(MemberVisibility.Public, nested.Visibility);
    }

    [Fact]
    public async Task LoadAsyncDescribesGenericTypesAndDelegates()
    {
        var assembly = await LoadFixtureAsync();

        Assert.Equal("<T>", Find(assembly, "SampleBase`1").GenericSuffix);
        Assert.Equal("Class : Object", Find(assembly, "SampleBase`1").TypeSummary);
        Assert.Equal("Delegate", Find(assembly, "SampleHandler").KindLabel);
    }

    private Task<AssemblyModel> LoadFixtureAsync() =>
        _provider.LoadAsync(new MetadataSource(FixturePath));

    private static ClassModel Find(AssemblyModel assembly, string name) =>
        Assert.Single(assembly.Classes, type => type.Name == name);

    [Fact]
    public async Task LoadAsyncUsesInvariantCultureForEnumValues()
    {
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
            var state = Find(await LoadFixtureAsync(), "SampleState");

            Assert.All(state.EnumMembers, member => Assert.DoesNotContain(" ", member.Value, StringComparison.Ordinal));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
