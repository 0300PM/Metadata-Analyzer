using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MetadataAnalyzer.WPF.ViewModels;

namespace MetadataAnalyzer.WPF.Converters;

public sealed class NodeTypeToIconConverter : IValueConverter
{
    private static readonly Dictionary<TreeNodeKind, string> Icons = new()
    {
        [TreeNodeKind.Assembly] = "pack://application:,,,/Resources/Icons/assembly.svg",
        [TreeNodeKind.Namespace] = "pack://application:,,,/Resources/Icons/namespace.svg",
        [TreeNodeKind.Class] = "pack://application:,,,/Resources/Icons/class.svg",
        [TreeNodeKind.Interface] = "pack://application:,,,/Resources/Icons/interface.svg",
        [TreeNodeKind.Struct] = "pack://application:,,,/Resources/Icons/struct.svg",
        [TreeNodeKind.Enum] = "pack://application:,,,/Resources/Icons/enum.svg",
        [TreeNodeKind.Method] = "pack://application:,,,/Resources/Icons/method.svg",
        [TreeNodeKind.Property] = "pack://application:,,,/Resources/Icons/property.svg",
        [TreeNodeKind.Field] = "pack://application:,,,/Resources/Icons/field.svg",
        [TreeNodeKind.Constructor] = "pack://application:,,,/Resources/Icons/constructor.svg",
        [TreeNodeKind.Parameter] = "pack://application:,,,/Resources/Icons/parameter.svg",
        [TreeNodeKind.Value] = "pack://application:,,,/Resources/Icons/value.svg",
    };

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is TreeNodeKind kind && Icons.TryGetValue(kind, out var icon)
            ? icon
            : DependencyProperty.UnsetValue;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
