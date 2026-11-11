using System.Windows;
using MetadataAnalyzer.WPF.ViewModels;

namespace MetadataAnalyzer.WPF.Views;

public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;

        viewModel.Closed += (_, _) => Close();
    }
}
