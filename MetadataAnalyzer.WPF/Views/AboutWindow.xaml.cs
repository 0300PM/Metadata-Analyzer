using System.Windows;
using MetadataAnalyzer.WPF.ViewModels;

namespace MetadataAnalyzer.WPF.Views;

public partial class AboutWindow : Window
{
    public AboutWindow(AboutViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
