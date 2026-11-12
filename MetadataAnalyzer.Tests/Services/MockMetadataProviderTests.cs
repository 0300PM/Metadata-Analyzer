using MetadataAnalyzer.Core.Models;
using MetadataAnalyzer.Core.Services;
using MetadataAnalyzer.Tests.Support;

namespace MetadataAnalyzer.Tests.Services;

public sealed class MockMetadataProviderTests
{
    private readonly MockMetadataProvider _provider = new(new FakeClock(DateTimeOffset.Parse("2026-11-10T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public async Task LoadAsyncReturnsSampleLibraryAssembly()
    {
        var assembly = await _provider.LoadAsync();

        Assert.Equal(MockMetadataProvider.AssemblyName, assembly.Name);
        Assert.Equal(MockMetadataProvider.AssemblyVersion, assembly.Version);
        Assert.Equal("SampleLibrary.dll", assembly.FileName);
    }

    [Fact]
    public async Task LoadAsyncReturnsExpectedStructure()
    {
        var assembly = await _provider.LoadAsync();

        Assert.Equal(3, assembly.NamespaceCount);
        Assert.Equal(8, assembly.ClassCount);
        Assert.Equal(
            ["SampleLibrary.Core", "SampleLibrary.Models", "SampleLibrary.Interface"],
            assembly.Namespaces.Select(@namespace => @namespace.Name));
    }

    [Fact]
    public async Task LoadAsyncContainsRequiredClasses()
    {
        var assembly = await _provider.LoadAsync();

        var user = Assert.Single(assembly.Classes, @class => @class.Name == "User");
        Assert.Equal("SampleLibrary.Models.User", user.FullName);
        Assert.Contains(user.Properties, property => property.Name == "Id" && property.PropertyType == "int");
        Assert.Contains(user.Properties, property => property.Name == "Name" && property.PropertyType == "string");
        Assert.Contains(user.Methods, method => method.Name == "GetFullName" && method.ReturnType == "string");

        var product = Assert.Single(assembly.Classes, @class => @class.Name == "Product");
        Assert.Contains(product.Properties, property => property.Name == "Price" && property.PropertyType == "decimal");
        Assert.Contains(product.Methods, method => method.Name == "CalculateTax" && method.ReturnType == "decimal");

        var service = Assert.Single(assembly.Classes, @class => @class.Name == "UserService");
        var getUser = Assert.Single(service.Methods, method => method.Name == "GetUser");
        Assert.Equal("User", getUser.ReturnType);
        Assert.Equal("int", Assert.Single(getUser.Parameters).ParameterType);

        var deleteUser = Assert.Single(service.Methods, method => method.Name == "DeleteUser");
        Assert.Equal("bool", deleteUser.ReturnType);

        var logger = Assert.Single(assembly.Classes, @class => @class.Name == "Logger");
        Assert.Contains(logger.Methods, method => method.Name == "Write" && method.IsStatic);
    }

    [Fact]
    public async Task LoadAsyncStampsAssemblyWithClockTime()
    {
        var assembly = await _provider.LoadAsync();

        Assert.Equal(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero), assembly.LoadedAt);
    }

    [Fact]
    public async Task LoadAsyncReturnsFreshInstances()
    {
        var first = await _provider.LoadAsync();
        var second = await _provider.LoadAsync();

        Assert.NotSame(first, second);
    }

    [Fact]
    public async Task LoadAsyncHonoursCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _provider.LoadAsync(cancellation.Token));
    }
}
