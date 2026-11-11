using MetadataAnalyzer.Core.Models;
using MetadataAnalyzer.Core.Services;

namespace MetadataAnalyzer.Tests.Services;

public sealed class MetadataFormatterTests
{
    private readonly MetadataFormatter _formatter = new();

    [Fact]
    public void FormatTreeRendersAssemblyHeader()
    {
        var text = _formatter.FormatTree(BuildSampleAssembly());

        Assert.StartsWith("Assembly: SampleLibrary", text, StringComparison.Ordinal);
        Assert.Contains("Version: 1.0.0.0", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatTreeRendersHierarchyWithIndentation()
    {
        var assembly = BuildSampleAssembly();
        var lines = _formatter.FormatTree(assembly).Split(Environment.NewLine);

        Assert.Contains("|   +-- Namespace: SampleLibrary.Models", lines);
        Assert.Contains("    +-- Class: User", lines);
        Assert.Contains(lines, line => line.EndsWith("+-- Property: Id (int)", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.EndsWith("+-- Method: GetFullName() : string", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("Namespace: SampleLibrary.Helpers", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.TrimEnd().EndsWith("Class: Logger", StringComparison.Ordinal));
    }

    [Fact]
    public void FormatTreeHandlesEmptyAssembly()
    {
        var text = _formatter.FormatTree(AssemblyModel.Empty());

        Assert.Contains("Assembly: Empty", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Namespace:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatTreeRejectsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _formatter.FormatTree(null!));
    }

    [Fact]
    public void FormatDetailsReportsCounters()
    {
        var text = _formatter.FormatDetails(BuildSampleAssembly());

        Assert.Contains("Namespaces:   2", text, StringComparison.Ordinal);
        Assert.Contains("Classes:      2", text, StringComparison.Ordinal);
        Assert.Contains("Members:      5", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatDetailsHandlesEmptyAssembly()
    {
        var text = _formatter.FormatDetails(AssemblyModel.Empty());

        Assert.Contains("Assembly contains no namespaces.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatMemberDetailsDescribesMethod()
    {
        var type = BuildSampleAssembly().Classes.First(@class => @class.Name == "User");

        var text = _formatter.FormatMemberDetails(type, "GetFullName");

        Assert.Contains("Kind:         Method", text, StringComparison.Ordinal);
        Assert.Contains("Return type:  string", text, StringComparison.Ordinal);
        Assert.Contains("Parameters:   none", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatMemberDetailsDescribesProperty()
    {
        var type = BuildSampleAssembly().Classes.First(@class => @class.Name == "User");

        var text = _formatter.FormatMemberDetails(type, "Name");

        Assert.Contains("Kind:         Property", text, StringComparison.Ordinal);
        Assert.Contains("Accessors:    get, set", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatMemberDetailsMarksUnknownMember()
    {
        var type = BuildSampleAssembly().Classes.First();

        var text = _formatter.FormatMemberDetails(type, "Missing");

        Assert.Contains("(not found)", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatMemberDetailsRejectsEmptyMemberName()
    {
        var type = BuildSampleAssembly().Classes.First();

        Assert.Throws<ArgumentException>(() => _formatter.FormatMemberDetails(type, " "));
    }

    [Fact]
    public void FormatTreeIsCultureInvariant()
    {
        var original = Thread.CurrentThread.CurrentCulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("pl-PL");
            var polish = _formatter.FormatTree(BuildSampleAssembly());

            Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            var invariant = _formatter.FormatTree(BuildSampleAssembly());

            Assert.Equal(invariant, polish);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = original;
        }
    }

    private static AssemblyModel BuildSampleAssembly()
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
            Methods: [new MethodModel("Write", "void", IsStatic: true, Parameters: [new ParameterModel("message", "string")])]);

        return new AssemblyModel(
            "SampleLibrary",
            "1.0.0.0",
            "SampleLibrary.dll",
            [
                new NamespaceModel("SampleLibrary.Models", [user]),
                new NamespaceModel("SampleLibrary.Helpers", [logger]),
            ],
            new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
    }
}
