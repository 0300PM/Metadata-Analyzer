using System.Collections;
using System.Windows;
using System.Windows.Controls;
using MetadataAnalyzer.WPF.ViewModels;

namespace MetadataAnalyzer.WPF.Controls;

public partial class AssemblyTreeView : UserControl
{
    public static readonly DependencyProperty SelectedNodeProperty = DependencyProperty.Register(
        nameof(SelectedNode),
        typeof(TreeNodeViewModel),
        typeof(AssemblyTreeView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedNodeChanged));

    public AssemblyTreeView()
    {
        InitializeComponent();
    }

    public TreeNodeViewModel? SelectedNode
    {
        get => (TreeNodeViewModel?)GetValue(SelectedNodeProperty);
        set => SetValue(SelectedNodeProperty, value);
    }

    private static void OnSelectedNodeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AssemblyTreeView view && e.NewValue is TreeNodeViewModel node)
        {
            view.SelectInTree(node);
        }
    }

    private void SelectInTree(TreeNodeViewModel node)
    {
        var parent = node.Parent;

        while (parent is not null)
        {
            parent.IsExpanded = true;
            parent = parent.Parent;
        }

        Tree.UpdateLayout();

        if (FindContainer(Tree, node) is { } container)
        {
            container.IsSelected = true;
            container.BringIntoView();
        }
    }

    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        SelectedNode = e.NewValue as TreeNodeViewModel;
        var label = DataContext is MainViewModel model ? model.SelectedNode?.Label ?? "null" : "n/a";
        var detailsTitle = DataContext is MainViewModel viewModel ? viewModel.Details.Title : "n/a";
        System.IO.File.AppendAllText(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tree-diag.txt"),
            "vm=" + label + " details=" + detailsTitle + Environment.NewLine);
    }

    private static TreeViewItem? FindContainer(ItemsControl parent, TreeNodeViewModel node)
    {
        if (parent is not ItemsControl control)
        {
            return null;
        }

        foreach (var item in control.Items)
        {
            if (control.ItemContainerGenerator.ContainerFromItem(item) is not TreeViewItem container)
            {
                continue;
            }

            if (ReferenceEquals(item, node))
            {
                return container;
            }

            var nested = FindContainer(container, node);

            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }
}
