using System.Globalization;
using MetadataAnalyzer.Core.Models;

namespace MetadataAnalyzer.CLI;

public sealed record MenuOption(string Key, string Title);

public static class ConsolePresentation
{
    public const int Width = 40;

    private const int LabelWidth = 14;

    public static IReadOnlyList<MenuOption> Menu { get; } =
    [
        new("1", "Load simulated metadata"),
        new("2", "Display assembly structure"),
        new("3", "Display assembly details"),
        new("4", "Display member details"),
        new("0", "Exit"),
    ];

    public static void WriteHeader(string title)
    {
        Console.WriteLine(new string('=', Width));
        var padding = Math.Max(0, (Width - title.Length) / 2);
        Console.WriteLine(new string(' ', padding) + title);
        Console.WriteLine(new string('=', Width));
        Console.WriteLine();
    }

    public static void WriteFooter(string message)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', Width));
        Console.WriteLine(message);
        Console.WriteLine(new string('=', Width));
    }

    public static void WriteMenu()
    {
        foreach (var option in Menu)
        {
            Console.WriteLine($"[{option.Key}] {option.Title}");
        }

        Console.WriteLine();
    }

    public static void WriteSuccess(string message) => WriteColored(message, ConsoleColor.Green);

    public static void WriteWarning(string message) => WriteColored(message, ConsoleColor.Yellow);

    public static void WriteError(string message) => WriteColored(message, ConsoleColor.Red);

    public static void WriteLabel(string label, string value) =>
        Console.WriteLine($"{label + ":",-LabelWidth} {value}");

    public static void WriteField(string label, string value)
    {
        var prefix = $"{label + ":",-LabelWidth} ";
        var lines = value.Split('\n');
        Console.WriteLine(prefix + lines[0]);

        for (var index = 1; index < lines.Length; index++)
        {
            Console.WriteLine(new string(' ', prefix.Length) + lines[index]);
        }
    }

    public static void WriteStatus(string state, AssemblyModel? assembly, TimeSpan? elapsed)
    {
        var namespaces = assembly?.NamespaceCount ?? 0;
        var classes = assembly?.ClassCount ?? 0;
        var duration = elapsed is null
            ? "-"
            : elapsed.Value.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) + " ms";

        Console.WriteLine(
            $"State: {state} | Namespaces: {namespaces} | Classes: {classes} | Last operation: {duration}");
    }

    public static void WriteTable(IReadOnlyList<(string Left, string Right)> rows)
    {
        var leftWidth = rows.Count == 0 ? 0 : rows.Max(row => row.Left.Length) + 2;

        foreach (var (left, right) in rows)
        {
            Console.WriteLine($"{left.PadRight(leftWidth)}{right}");
        }
    }

    private static void WriteColored(string message, ConsoleColor color)
    {
        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = previous;
    }
}
