using System.Globalization;
using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Core.Models;
using MetadataAnalyzer.Core.Services;

namespace MetadataAnalyzer.CLI;

public sealed class MetadataConsoleApp(
    IMetadataService metadataService,
    IMetadataFormatter formatter,
    IAppLogger logger,
    TextReader input,
    TextWriter output)
{
    private AssemblyModel? _assembly;
    private TimeSpan? _lastOperation;

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        ConsolePresentation.WriteHeader("METADATA ANALYZER");
        await logger.LogInformationAsync(LogEvents.ApplicationStarted).ConfigureAwait(false);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ConsolePresentation.WriteMenu();
                ConsolePresentation.WriteStatus(DescribeState(), _assembly, _lastOperation);
                output.Write("Select option: ");

                var choice = input.ReadLine();
                if (choice is null)
                {
                    output.WriteLine();
                    return 0;
                }

                output.WriteLine();

                if (choice.Trim() == "0")
                {
                    ConsolePresentation.WriteFooter("Goodbye.");
                    return 0;
                }

                await HandleAsync(choice.Trim(), cancellationToken).ConfigureAwait(false);
                output.WriteLine();
            }

            return 0;
        }
        finally
        {
            await logger.LogInformationAsync(LogEvents.ApplicationClosed).ConfigureAwait(false);
        }
    }

    private async Task HandleAsync(string choice, CancellationToken cancellationToken)
    {
        switch (choice)
        {
            case "1":
                await LoadAsync(cancellationToken).ConfigureAwait(false);
                break;
            case "2":
                DisplayStructure();
                break;
            case "3":
                DisplayDetails();
                break;
            case "4":
                DisplayMemberDetails();
                break;
            default:
                await HandleInvalidOptionAsync(choice).ConfigureAwait(false);
                break;
        }
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            _assembly = await metadataService.AnalyzeAsync(cancellationToken).ConfigureAwait(false);
            _lastOperation = DateTimeOffset.UtcNow - startedAt;

            ConsolePresentation.WriteSuccess($"Metadata loaded: {_assembly.Name} {_assembly.Version}");
            ConsolePresentation.WriteLabel("File name", _assembly.FileName);
            ConsolePresentation.WriteLabel("Namespaces", _assembly.NamespaceCount.ToString(CultureInfo.InvariantCulture));
            ConsolePresentation.WriteLabel("Classes", _assembly.ClassCount.ToString(CultureInfo.InvariantCulture));
        }
        catch (OperationCanceledException)
        {
            ConsolePresentation.WriteWarning("Loading cancelled.");
        }
        catch (Exception exception)
        {
            await logger.LogErrorAsync($"{LogEvents.MetadataAnalysisFailed}: {exception.Message}")
                .ConfigureAwait(false);

            ConsolePresentation.WriteError($"Analysis failed: {exception.Message}");
        }
    }

    private void DisplayStructure()
    {
        if (!TryGetAssembly(out var assembly))
        {
            return;
        }

        output.WriteLine(formatter.FormatTree(assembly));
        output.WriteLine();
        ConsolePresentation.WriteSuccess("Analysis completed successfully.");
    }

    private void DisplayDetails()
    {
        if (!TryGetAssembly(out var assembly))
        {
            return;
        }

        output.WriteLine(formatter.FormatDetails(assembly));
    }

    private void DisplayMemberDetails()
    {
        if (!TryGetAssembly(out var assembly))
        {
            return;
        }

        var classes = assembly.Classes.ToList();
        if (classes.Count == 0)
        {
            ConsolePresentation.WriteWarning("The loaded assembly contains no classes.");
            return;
        }

        ConsolePresentation.WriteTable(classes
            .Select((type, index) => ($"[{index + 1}]", type.FullName))
            .ToList());

        if (!TryReadSelection("Select class: ", classes.Count, out var classIndex))
        {
            return;
        }

        var selected = classes[classIndex];
        var members = EnumerateMemberNames(selected).ToList();

        if (members.Count == 0)
        {
            ConsolePresentation.WriteWarning($"Class {selected.Name} has no members.");
            return;
        }

        output.WriteLine();
        ConsolePresentation.WriteTable(members
            .Select((member, index) => ($"[{index + 1}]", member))
            .ToList());

        if (!TryReadSelection("Select member: ", members.Count, out var memberIndex))
        {
            return;
        }

        output.WriteLine();
        output.WriteLine(formatter.FormatMemberDetails(selected, members[memberIndex]));
    }

    private bool TryReadSelection(string prompt, int count, out int index)
    {
        output.Write(prompt);

        if (TryParseIndex(input.ReadLine(), count, out index))
        {
            return true;
        }

        ConsolePresentation.WriteWarning($"Invalid number. Enter a value between 1 and {count}.");
        return false;
    }

    private bool TryGetAssembly([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out AssemblyModel? assembly)
    {
        assembly = _assembly;

        if (assembly is not null)
        {
            return true;
        }

        ConsolePresentation.WriteWarning("No metadata loaded. Select option 1 first.");
        return false;
    }

    private async Task HandleInvalidOptionAsync(string choice)
    {
        ConsolePresentation.WriteWarning($"Invalid option '{choice}'. Choose one of: 1, 2, 3, 4, 0.");
        await logger.LogWarningAsync($"{LogEvents.InvalidSelection}: '{choice}'").ConfigureAwait(false);
    }

    private static IEnumerable<string> EnumerateMemberNames(ClassModel type)
    {
        foreach (var field in type.Fields)
        {
            yield return field.Name;
        }

        foreach (var property in type.Properties)
        {
            yield return property.Name;
        }

        foreach (var constructor in type.Constructors)
        {
            yield return $"{type.Name}{constructor.Signature}";
        }

        foreach (var method in type.Methods)
        {
            yield return method.Name;
        }
    }

    private static bool TryParseIndex(string? value, int count, out int index)
    {
        var parsed = int.TryParse(value?.Trim(), CultureInfo.InvariantCulture, out var number)
            && number >= 1
            && number <= count;

        index = parsed ? number - 1 : -1;

        return parsed;
    }

    private string DescribeState() => _assembly is null ? "Ready (no metadata loaded)" : "Metadata loaded";
}
