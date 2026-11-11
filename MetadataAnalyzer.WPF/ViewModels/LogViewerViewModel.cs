using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MetadataAnalyzer.Infrastructure.Logging;

namespace MetadataAnalyzer.WPF.ViewModels;

public sealed partial class LogViewerViewModel : ObservableObject
{
    private readonly InMemoryLogSink _sink;

    [ObservableProperty]
    private string _levelFilter = "All";

    [ObservableProperty]
    private bool _autoScroll = true;

    [ObservableProperty]
    private string _counters = "0 entries";

    public LogViewerViewModel(InMemoryLogSink sink)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        Entries = CollectionViewSource.GetDefaultView(_sink.Entries);
        Entries.Filter = MatchesFilter;
        Entries.CollectionChanged += (_, _) => UpdateCounters();
        UpdateCounters();
    }

    public IReadOnlyList<string> Levels { get; } = ["All", "Information", "Warning", "Error"];

    public ICollectionView Entries { get; }

    [RelayCommand]
    private void Clear()
    {
        _sink.Clear();
        UpdateCounters();
    }

    [RelayCommand]
    private void SelectLevel(string? level)
    {
        LevelFilter = string.IsNullOrEmpty(level) ? "All" : level;
    }

    partial void OnLevelFilterChanged(string value)
    {
        Entries.Refresh();
        UpdateCounters();
    }

    private bool MatchesFilter(object item) =>
        item is LogEntry entry
        && (LevelFilter == "All" || string.Equals(entry.Level, LevelFilter, StringComparison.Ordinal));

    private void UpdateCounters()
    {
        var visible = _sink.Entries.Count(MatchesFilter);
        var total = _sink.Entries.Count;

        Counters = LevelFilter == "All"
            ? $"{total} entries"
            : $"{visible} of {total} entries";
    }
}
