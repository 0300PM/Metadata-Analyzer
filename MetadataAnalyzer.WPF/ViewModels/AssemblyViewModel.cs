using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.WPF.ViewModels;

public sealed class AssemblyViewModel : ObservableObject
{
    private AssemblyViewModel(
        string name,
        string version,
        string fileName,
        string loadedAt,
        TreeNodeViewModel root,
        int namespaceCount,
        int classCount,
        int memberCount)
    {
        Name = name;
        Version = version;
        FileName = fileName;
        LoadedAt = loadedAt;
        Root = root;
        Nodes = [root];
        NamespaceCount = namespaceCount;
        ClassCount = classCount;
        MemberCount = memberCount;
    }

    public string Name { get; }

    public string Version { get; }

    public string FileName { get; }

    public string LoadedAt { get; }

    public TreeNodeViewModel Root { get; }

    public ObservableCollection<TreeNodeViewModel> Nodes { get; }

    public int NamespaceCount { get; }

    public int ClassCount { get; }

    public int MemberCount { get; }

    public string Summary => $"{FileName} · v{Version}";

    public string Counters => $"{Describe(NamespaceCount)} namespaces · {Describe(ClassCount)} types · {Describe(MemberCount)} members";

    public static AssemblyViewModel FromModel(AssemblyModel assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return new AssemblyViewModel(
            assembly.Name,
            assembly.Version,
            assembly.FileName,
            assembly.LoadedAt == DateTimeOffset.MinValue
                ? "-"
                : assembly.LoadedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            TreeNodeViewModel.FromAssembly(assembly),
            assembly.NamespaceCount,
            assembly.ClassCount,
            assembly.MemberCount);
    }

    public void SetExpanded(bool expanded) => Root.SetExpandedRecursively(expanded);

    private static string Describe(int value) => value.ToString(CultureInfo.InvariantCulture);
}
