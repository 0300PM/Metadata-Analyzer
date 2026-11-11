using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MetadataAnalyzer.Infrastructure.Logging;

namespace MetadataAnalyzer.WPF.Converters;

public sealed class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value is string level
            ? level switch
            {
                "Error" => "DangerBrush",
                "Warning" => "WarningBrush",
                _ => "TextSecondaryBrush",
            }
            : "TextSecondaryBrush";

        return Application.Current?.TryFindResource(key) ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public sealed class LogEntryToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is LogEntry entry ? $"[{entry.Date} {entry.Time}] {entry.Level.ToUpperInvariant(),-11} {entry.Message}" : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
