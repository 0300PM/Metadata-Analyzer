using System.Windows;
using MetadataAnalyzer.WPF.ViewModels;

namespace MetadataAnalyzer.WPF.Views;

public partial class LogViewerWindow : Window
{
    public LogViewerWindow(LogViewerViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
