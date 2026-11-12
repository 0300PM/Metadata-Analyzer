using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;
using MetadataAnalyzer.Core.Services;
using MetadataAnalyzer.Infrastructure.Configuration;
using MetadataAnalyzer.Infrastructure.Logging;

namespace MetadataAnalyzer.WPF.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private const string AppName = "Metadata Analyzer";

    private readonly IMetadataService _metadataService;
    private readonly IAppLogger _logger;
    private readonly ISystemClock _clock;
    private readonly InMemoryLogSink? _logSink;
    private readonly AppSettings _settings;
    private Func<string?>? _pickAssemblyFile;

    [ObservableProperty]
    private AssemblyViewModel? _assembly;

    [ObservableProperty]
    private TreeNodeViewModel? _selectedNode;

    [ObservableProperty]
    private DetailsViewModel _details = DetailsViewModel.Empty;

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isDetailsVisible = true;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public MainViewModel(
        IMetadataService metadataService,
        IAppLogger logger,
        ISystemClock clock,
        InMemoryLogSink? logSink = null,
        AppSettings? settings = null)
    {
        _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logSink = logSink;
        _settings = settings ?? AppSettings.Default;
        _isDetailsVisible = _settings.ShowDetailsPanel;
    }

    public event EventHandler? SettingsRequested;

    public event EventHandler? AboutRequested;

    public event EventHandler? LogViewerRequested;

    public string Title { get; } = $"{AppName} {ApplicationOptions.DefaultVersion}";

    public string Stage => $"Stage 1 · v{ApplicationOptions.DefaultVersion}, log level {_settings.LogLevel}";

    public bool HasMetadata => Assembly is not null;

    public bool HasSelection => SelectedNode is not null;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public bool IsEmpty => Assembly is null && !IsBusy;

    public bool CanUseTree => Assembly is not null && !IsBusy;

    public bool HasSearch => !string.IsNullOrWhiteSpace(SearchText);

    public int MatchCount => Assembly?.MatchCount ?? 0;

    public string Subtitle => Assembly?.Summary ?? "No assembly loaded";

    public string Counters => Assembly?.Counters ?? "0 namespaces · 0 types · 0 members";

    public string SelectionSummary => SelectedNode is null
        ? "No element selected"
        : $"{Details.Kind}: {Details.Title}";

    public IReadOnlyList<LogEntry> LogEntries => _logSink?.Entries ?? [];

    [RelayCommand]
    private async Task OpenAssemblyAsync(CancellationToken cancellationToken)
    {
        var path = _pickAssemblyFile?.Invoke();

        if (path is null)
        {
            StatusMessage = "Open cancelled.";
            return;
        }

        await LoadAsync(
                token => _metadataService.AnalyzeAsync(new MetadataSource(path), token),
                "Open",
                cancellationToken)
            .ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task LoadSampleAsync(CancellationToken cancellationToken) =>
        await LoadAsync(
                token => _metadataService.AnalyzeAsync(MetadataSource.Empty, token),
                "Load sample",
                cancellationToken)
            .ConfigureAwait(true);

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken) =>
        await LoadAsync(_metadataService.RefreshAsync, "Refresh", cancellationToken).ConfigureAwait(true);

    [RelayCommand]
    private async Task CloseAssemblyAsync()
    {
        Assembly = null;
        SelectedNode = null;
        Details = DetailsViewModel.Empty;
        ErrorMessage = null;
        StatusMessage = "Assembly closed.";
        RaiseState();

        await _logger.LogInformationAsync("Assembly closed").ConfigureAwait(true);
    }

    [RelayCommand]
    private void ExpandAll() => SetExpanded(true);

    [RelayCommand]
    private void CollapseAll() => SetExpanded(false);

    [RelayCommand]
    private void ToggleDetails() => IsDetailsVisible = !IsDetailsVisible;

    [RelayCommand]
    private void ShowSettings() => SettingsRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ShowAbout() => AboutRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void ShowLogViewer() => LogViewerRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task ClearLogsAsync()
    {
        _logSink?.Clear();
        StatusMessage = "Log view cleared.";
        OnPropertyChanged(nameof(LogEntries));

        await _logger.LogInformationAsync("Logs cleared").ConfigureAwait(true);
    }

    [RelayCommand]
    private void ClearSelection() => SelectedNode = null;

    public void SelectNode(TreeNodeViewModel? node) => SelectedNode = node;

    public void UseAssemblyPicker(Func<string?> pickAssemblyFile) =>
        _pickAssemblyFile = pickAssemblyFile ?? throw new ArgumentNullException(nameof(pickAssemblyFile));

    /// <summary>
    /// Pokazuje błąd w pasku ostrzeżeń zamiast kończyć proces.
    /// </summary>
    public void ReportError(string message)
    {
        ErrorMessage = message;
        StatusMessage = "An error occurred. See the log for details.";
    }

    partial void OnSelectedNodeChanged(TreeNodeViewModel? value)
    {
        Details = value?.ToDetails() ?? DetailsViewModel.Empty;
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectionSummary));

        if (value is not null)
        {
            StatusMessage = $"{Details.Kind} selected: {Details.Title}";
        }
    }

    partial void OnIsDetailsVisibleChanged(bool value)
    {
        if (!value)
        {
            StatusMessage = "Details panel hidden.";
        }
        else
        {
            StatusMessage = Assembly is null ? "Ready" : "Details panel shown.";
        }
    }

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasSearch));

        if (Assembly is null)
        {
            return;
        }

        Assembly.ApplyFilter(value);
        OnPropertyChanged(nameof(MatchCount));

        StatusMessage = HasSearch
            ? $"{MatchCount} matching elements for \"{value.Trim()}\"."
            : "Filter cleared.";
    }

    partial void OnIsBusyChanged(bool value) => RaiseState();

    partial void OnAssemblyChanged(AssemblyViewModel? value) => RaiseState();

    private void SetExpanded(bool expanded)
    {
        if (Assembly is null)
        {
            return;
        }

        Assembly.SetExpanded(expanded);
        StatusMessage = expanded ? "All elements expanded." : "All elements collapsed.";
    }

    private async Task LoadAsync(
        Func<CancellationToken, Task<AssemblyModel>> load,
        string operationName,
        CancellationToken cancellationToken)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = "Loading data...";

        var startedAt = _clock.Now;

        try
        {
            var model = await load(cancellationToken).ConfigureAwait(true);

            Assembly = AssemblyViewModel.FromModel(model);
            SelectedNode = null;
            Details = DetailsViewModel.FromAssembly(model);

            if (HasSearch)
            {
                Assembly.ApplyFilter(SearchText);
                OnPropertyChanged(nameof(MatchCount));
            }

            var elapsed = _clock.Now - startedAt;
            StatusMessage = $"Data loaded successfully. {operationName} completed in {elapsed.TotalMilliseconds:0} ms.";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = $"{operationName} cancelled.";
            await _logger.LogWarningAsync($"{operationName} cancelled").ConfigureAwait(true);
        }
        catch (MetadataServiceException exception)
        {
            ErrorMessage = $"{operationName} failed: {exception.UserMessage}";
            StatusMessage = "An error occurred. See the log for details.";
            await _logger.LogErrorAsync($"{operationName} failed: {exception.UserMessage}", exception).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            ErrorMessage = $"{operationName} failed: {exception.Message}";
            StatusMessage = "An error occurred. See the log for details.";
            await _logger.LogErrorAsync($"{operationName} failed: {exception.Message}", exception).ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            RaiseState();
        }
    }

    private void RaiseState()
    {
        OnPropertyChanged(nameof(HasMetadata));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(CanUseTree));
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(Counters));
        OnPropertyChanged(nameof(HasSearch));
        OnPropertyChanged(nameof(MatchCount));
    }
}
