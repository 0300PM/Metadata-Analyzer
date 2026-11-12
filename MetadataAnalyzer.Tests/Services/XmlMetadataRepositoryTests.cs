using System.Globalization;
using System.IO;
using MetadataAnalyzer.Core.Models;
using MetadataAnalyzer.Core.Services;
using MetadataAnalyzer.Tests.Support;

namespace MetadataAnalyzer.Tests.Services;

public sealed class XmlMetadataRepositoryTests : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    private XmlMetadataRepository CreateRepository(XmlMetadataRepositoryOptions? options = null) =>
        new(options ?? new XmlMetadataRepositoryOptions { DirectoryPath = _directory.Path });

    private static AssemblyModel Sample() =>
        MockMetadataProvider.BuildAssembly(new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.FromHours(2)));

    [Fact]
    public async Task SaveAsyncWritesUtf8XmlDocument()
    {
        using var repository = CreateRepository();
        var assembly = Sample();

        await repository.SaveAsync(assembly);

        var path = Path.Combine(_directory.Path, assembly.Name + ".xml");
        var content = await File.ReadAllTextAsync(path);

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", content, StringComparison.Ordinal);
        Assert.Contains("<metadataAnalysis", content, StringComparison.Ordinal);
        Assert.Contains("schemaVersion=\"1.0\"", content, StringComparison.Ordinal);
        Assert.Contains($"<Assembly name=\"{assembly.Name}\"", content, StringComparison.Ordinal);
        Assert.DoesNotContain(".tmp", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsyncCreatesDirectoryAutomatically()
    {
        var nested = Path.Combine(_directory.Path, "deep", "nested");
        using var repository = CreateRepository(new XmlMetadataRepositoryOptions { DirectoryPath = nested });

        Assert.False(repository.Exists);

        await repository.SaveAsync(Sample());

        Assert.True(repository.Exists);
        Assert.True(File.Exists(Path.Combine(nested, "SampleLibrary.xml")));
    }

    [Fact]
    public async Task SaveAsyncFailsWhenDirectoryIsMissingAndCreationIsDisabled()
    {
        var missing = Path.Combine(_directory.Path, "missing");
        using var repository = CreateRepository(new XmlMetadataRepositoryOptions
        {
            DirectoryPath = missing,
            CreateDirectory = false,
        });

        await Assert.ThrowsAsync<RepositoryDocumentException>(() => repository.SaveAsync(Sample()));
    }

    [Fact]
    public async Task LoadAsyncRestoresCompleteModel()
    {
        using var repository = CreateRepository();
        var assembly = Sample();

        await repository.SaveAsync(assembly);
        var restored = await repository.LoadAsync(assembly.Name);

        Assert.NotNull(restored);
        Assert.Empty(AssemblyModelComparer.Describe(assembly, restored));
    }

    [Fact]
    public async Task LoadAsyncReturnsNullForUnknownAssembly()
    {
        using var repository = CreateRepository();

        Assert.Null(await repository.LoadAsync("Missing"));
    }

    [Fact]
    public async Task SaveAsyncOverwritesExistingAnalysisWithoutDuplicates()
    {
        using var repository = CreateRepository();
        var assembly = Sample();

        await repository.SaveAsync(assembly);
        await repository.SaveAsync(assembly with { Version = "2.0.0.0" });
        var restored = await repository.LoadAsync(assembly.Name);

        Assert.NotNull(restored);
        Assert.Equal("2.0.0.0", restored.Version);
        Assert.Equal([assembly.Name], await repository.ListAsync());
        Assert.Single(Directory.EnumerateFiles(_directory.Path, "*.xml"));
    }

    [Fact]
    public async Task SaveAsyncKeepsPreviousFileWhenSerializationFails()
    {
        using var repository = CreateRepository();
        var assembly = Sample();
        await repository.SaveAsync(assembly);

        var path = Path.Combine(_directory.Path, assembly.Name + ".xml");
        var before = await File.ReadAllTextAsync(path);

        using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await Assert.ThrowsAsync<RepositoryDocumentException>(() => repository.SaveAsync(assembly with { Version = "3.0.0.0" }));
        }

        Assert.Equal(before, await File.ReadAllTextAsync(path));
        Assert.Empty(Directory.EnumerateFiles(_directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task LoadAsyncRejectsCorruptedDocument()
    {
        using var repository = CreateRepository();
        var assembly = Sample();
        await repository.SaveAsync(assembly);

        await File.WriteAllTextAsync(Path.Combine(_directory.Path, assembly.Name + ".xml"), "<metadataAnalysis><broken>");

        await Assert.ThrowsAsync<RepositoryDocumentException>(() => repository.LoadAsync(assembly.Name));
    }

    [Fact]
    public async Task LoadAsyncRejectsForeignXml()
    {
        using var repository = CreateRepository();
        var assembly = Sample();
        await repository.SaveAsync(assembly);

        await File.WriteAllTextAsync(Path.Combine(_directory.Path, assembly.Name + ".xml"), "<?xml version=\"1.0\"?><other />");

        await Assert.ThrowsAsync<RepositoryDocumentException>(() => repository.LoadAsync(assembly.Name));
    }

    [Fact]
    public async Task ListAsyncIgnoresForeignAndTemporaryFiles()
    {
        using var repository = CreateRepository();
        await repository.SaveAsync(Sample());
        await File.WriteAllTextAsync(Path.Combine(_directory.Path, "notes.xml"), "<other />");
        await File.WriteAllTextAsync(Path.Combine(_directory.Path, "SampleLibrary.xml.tmp"), "<metadataAnalysis />");
        await File.WriteAllTextAsync(Path.Combine(_directory.Path, "readme.txt"), "text");

        Assert.Equal(["SampleLibrary"], await repository.ListAsync());
    }

    [Fact]
    public async Task ListAsyncReturnsEmptyForMissingDirectory()
    {
        using var repository = CreateRepository(new XmlMetadataRepositoryOptions
        {
            DirectoryPath = Path.Combine(_directory.Path, "absent"),
        });

        Assert.Empty(await repository.ListAsync());
        Assert.Null(await repository.LoadLatestAsync());
    }

    [Fact]
    public async Task LoadLatestAsyncPicksNewestAnalysis()
    {
        using var repository = CreateRepository();
        var older = Sample() with { Name = "Older", LoadedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero) };
        var newer = Sample() with { Name = "Newer", LoadedAt = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero) };

        await repository.SaveAsync(older);
        await repository.SaveAsync(newer);

        var latest = await repository.LoadLatestAsync();

        Assert.NotNull(latest);
        Assert.Equal("Newer", latest.Name);
    }

    [Fact]
    public async Task DeleteAsyncRemovesFileAndReportsResult()
    {
        using var repository = CreateRepository();
        var assembly = Sample();
        await repository.SaveAsync(assembly);

        Assert.True(await repository.DeleteAsync(assembly.Name));
        Assert.False(await repository.DeleteAsync(assembly.Name));
        Assert.Null(await repository.LoadAsync(assembly.Name));
    }

    [Fact]
    public async Task SaveAsyncSanitisesAssemblyName()
    {
        using var repository = CreateRepository();
        var assembly = Sample() with { Name = "Sample:Library/1" };

        await repository.SaveAsync(assembly);

        Assert.Equal(["Sample_Library_1"], await repository.ListAsync());
        Assert.Equal(assembly.Name, (await repository.LoadAsync(assembly.Name))!.Name);
    }

    [Fact]
    public async Task RepositoryHonoursCancellation()
    {
        using var repository = CreateRepository();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SaveAsync(Sample(), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.LoadAsync("SampleLibrary", cancellation.Token));
    }

    [Fact]
    public void DefaultsTargetApplicationDirectory()
    {
        using var repository = new XmlMetadataRepository();

        Assert.Equal("xml", repository.Name);
        Assert.Equal(Path.Combine(AppContext.BaseDirectory, "Repository"), repository.DirectoryPath);
    }

    [Fact]
    public void DirectoryPathIsResolvedAgainstApplicationBase()
    {
        using var repository = CreateRepository(new XmlMetadataRepositoryOptions { DirectoryPath = "Relative" });

        Assert.Equal(Path.GetFullPath("Relative", AppContext.BaseDirectory), repository.DirectoryPath);
    }

    [Fact]
    public async Task SaveAsyncWritesInvariantTimestamps()
    {
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("pl-PL");
            using var repository = CreateRepository();
            await repository.SaveAsync(Sample());

            var content = await File.ReadAllTextAsync(Path.Combine(_directory.Path, "SampleLibrary.xml"));

            Assert.Contains("loadedAt=\"2026-05-06T07:08:09", content, StringComparison.Ordinal);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    public void Dispose() => _directory.Dispose();
}
