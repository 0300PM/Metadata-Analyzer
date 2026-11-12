using MetadataAnalyzer.Core.Models;
using MetadataAnalyzer.Core.Services;
using MetadataAnalyzer.Tests.Support;
using MetadataAnalyzer.WPF.ViewModels;
using Moq;

namespace MetadataAnalyzer.Tests.ViewModels;

public sealed class MainViewModelTests
{
    private readonly Mock<Core.Interfaces.IMetadataService> _service = new();
    private readonly RecordingLogger _logger = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public void StartsWithoutMetadata()
    {
        var viewModel = CreateViewModel();

        Assert.False(viewModel.HasMetadata);
        Assert.Null(viewModel.Assembly);
        Assert.Same(DetailsViewModel.Empty, viewModel.Details);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task AnalyzePopulatesTreeAndDetails()
    {
        var assembly = await new MockMetadataProvider().LoadAsync();
        _service
            .Setup(service => service.AnalyzeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(assembly);

        var viewModel = CreateViewModel();
        await viewModel.OpenAssemblyCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasMetadata);
        Assert.NotNull(viewModel.Assembly);
        Assert.Equal(3, viewModel.Assembly!.NamespaceCount);
        Assert.Equal(8, viewModel.Assembly.ClassCount);

        var root = Assert.Single(viewModel.Assembly.Nodes);
        Assert.Equal(TreeNodeKind.Assembly, root.Kind);
        Assert.True(root.IsExpanded);
        Assert.Equal(3, root.Children.Count);
        Assert.Equal(TreeNodeKind.Namespace, root.Children[0].Kind);
        Assert.All(root.Children, node => Assert.True(node.HasChildren));
    }

    [Fact]
    public async Task AnalyzeSetsStatusMessageWithElapsedTime()
    {
        _service
            .Setup(service => service.AnalyzeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AssemblyModel.Empty());

        var viewModel = CreateViewModel();
        await viewModel.OpenAssemblyCommand.ExecuteAsync(null);

        Assert.Contains("Open completed in 0 ms", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task SelectingNodeUpdatesDetailsWithoutReloading()
    {
        var assembly = await new MockMetadataProvider().LoadAsync();
        _service
            .Setup(service => service.AnalyzeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(assembly);

        var viewModel = CreateViewModel();
        await viewModel.OpenAssemblyCommand.ExecuteAsync(null);

        var classNode = viewModel.Assembly!.Nodes[0].Children[1].Children
            .First(node => node.Label == "User");
        viewModel.SelectedNode = classNode;

        Assert.Equal("User", viewModel.Details.Title);
        Assert.Equal("Class", viewModel.Details.Kind);
        Assert.True(viewModel.Details.HasSelection);
        _service.Verify(service => service.AnalyzeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelectingMemberNodeShowsMemberDetails()
    {
        var assembly = await new MockMetadataProvider().LoadAsync();
        _service
            .Setup(service => service.AnalyzeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(assembly);

        var viewModel = CreateViewModel();
        await viewModel.OpenAssemblyCommand.ExecuteAsync(null);

        var models = viewModel.Assembly!.Nodes[0].Children[1];
        var userClass = models.Children.First(node => node.Label == "User");
        var methodNode = userClass.Children.First(node => node.Kind == TreeNodeKind.Method && node.Label.StartsWith("GetFullName", StringComparison.Ordinal));

        viewModel.SelectedNode = methodNode;

        Assert.Equal("Method", viewModel.Details.Kind);
        Assert.Equal("GetFullName", viewModel.Details.Title);
        Assert.Contains(viewModel.Details.Properties, item => item.Name == "Return Type" && item.Type == "string");
    }

    [Fact]
    public async Task ClearingSelectionResetsDetails()
    {
        _service
            .Setup(service => service.AnalyzeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(await new MockMetadataProvider().LoadAsync());

        var viewModel = CreateViewModel();
        await viewModel.OpenAssemblyCommand.ExecuteAsync(null);
        viewModel.SelectedNode = viewModel.Assembly!.Nodes[0];

        viewModel.ClearSelectionCommand.Execute(null);

        Assert.Null(viewModel.SelectedNode);
        Assert.False(viewModel.Details.HasSelection);
    }

    [Fact]
    public async Task AnalyzeFailureShowsErrorAndLogsIt()
    {
        _service
            .Setup(service => service.AnalyzeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var viewModel = CreateViewModel();
        await viewModel.OpenAssemblyCommand.ExecuteAsync(null);

        Assert.Contains("Open failed: boom", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains(_logger.Errors, entry => entry.Contains("boom", StringComparison.Ordinal));
        Assert.False(viewModel.HasMetadata);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task RefreshUsesRefreshOperation()
    {
        _service
            .Setup(service => service.RefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(await new MockMetadataProvider().LoadAsync());

        var viewModel = CreateViewModel();
        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.True(viewModel.HasMetadata);
        Assert.Contains("Refresh completed", viewModel.StatusMessage, StringComparison.Ordinal);
        _service.Verify(service => service.RefreshAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AnalyzeIsIgnoredWhileBusy()
    {
        var completion = new TaskCompletionSource<AssemblyModel>();
        _service
            .Setup(service => service.AnalyzeAsync(It.IsAny<CancellationToken>()))
            .Returns(completion.Task);

        var viewModel = CreateViewModel();
        var firstRun = viewModel.OpenAssemblyCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsBusy);

        await viewModel.OpenAssemblyCommand.ExecuteAsync(null);
        completion.SetResult(AssemblyModel.Empty());
        await firstRun;

        _service.Verify(service => service.AnalyzeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private MainViewModel CreateViewModel() => new(_service.Object, _logger, _clock);
}
