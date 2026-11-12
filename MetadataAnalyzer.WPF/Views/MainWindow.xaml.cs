using System.IO;
using System.Windows;
using MetadataAnalyzer.WPF.ViewModels;
using Microsoft.Win32;

namespace MetadataAnalyzer.WPF.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
        viewModel.UseAssemblyPicker(PickAssemblyFile);
    }

    private static string? PickAssemblyFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open assembly",
            Filter = "Assembly files (*.dll;*.exe)|*.dll;*.exe|All files (*.*)|*.*",
            CheckFileExists = true,
        };

        var directory = AppContext.BaseDirectory;

        if (Directory.Exists(directory))
        {
            dialog.InitialDirectory = directory;
        }

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private void OnExitClick(object sender, RoutedEventArgs e) => Close();
}
