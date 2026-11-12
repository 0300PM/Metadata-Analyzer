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

    public async Task<int> RunAsync(string? assemblyPath = null, CancellationToken cancellationToken = default)
    {
        ConsolePresentation.WriteHeader("METADATA ANALYZER");
        await logger.LogInformationAsync(LogEvents.ApplicationStarted).ConfigureAwait(false);

        try
        {
            if (!string.IsNullOrWhiteSpace(assemblyPath))
            {
                await AnalyzeAsync(assemblyPath.Trim(), cancellationToken).ConfigureAwait(false);
                output.WriteLine();
            }

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
            case "5":
                await AnalyzeFileAsync(cancellationToken).ConfigureAwait(false);
                break;
            case "6":
                await SaveAsync(cancellationToken).ConfigureAwait(false);
                break;
            case "7":
                await LoadFromRepositoryAsync(cancellationToken).ConfigureAwait(false);
                break;
            case "8":
                await RefreshAsync(cancellationToken).ConfigureAwait(false);
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

    private async Task AnalyzeFileAsync(CancellationToken cancellationToken)
    {
        output.Write("Assembly path: ");
        var path = input.ReadLine();

        if (string.IsNullOrWhiteSpace(path))
        {
            ConsolePresentation.WriteWarning("Assembly path cannot be empty.");
            return;
        }

        await AnalyzeAsync(path.Trim(), cancellationToken).ConfigureAwait(false);
    }

    private async Task AnalyzeAsync(string path, CancellationToken cancellationToken)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var source = new MetadataSource(path);

        try
        {
            _assembly = await metadataService.AnalyzeAsync(source, cancellationToken).ConfigureAwait(false);

            _lastOperation = DateTimeOffset.UtcNow - startedAt;

            ConsolePresentation.WriteSuccess($"Assembly analyzed: {_assembly.Name} {_assembly.Version}");
            ConsolePresentation.WriteLabel("Location", _assembly.FilePath ?? "(none)");
            ConsolePresentation.WriteLabel("Namespaces", _assembly.NamespaceCount.ToString(CultureInfo.InvariantCulture));
            ConsolePresentation.WriteLabel("Types", _assembly.ClassCount.ToString(CultureInfo.InvariantCulture));
        }
        catch (OperationCanceledException)
        {
            ConsolePresentation.WriteWarning("Analysis cancelled.");
        }
        catch (Exception exception)
        {
            await logger.LogErrorAsync($"{LogEvents.MetadataAnalysisFailed}: {exception.Message}")
                .ConfigureAwait(false);

            ConsolePresentation.WriteError($"Analysis failed: {exception.Message}");
        }
    }

    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        var source = metadataService.Source;

        if (source.FilePath is null)
        {
            ConsolePresentation.WriteWarning("Refresh requires an assembly analyzed from a file.");
            return;
        }

        var startedAt = DateTimeOffset.UtcNow;

        try
        {
            _assembly = await metadataService.RefreshAsync(cancellationToken).ConfigureAwait(false);
            _lastOperation = DateTimeOffset.UtcNow - startedAt;

            ConsolePresentation.WriteSuccess($"Analysis refreshed: {_assembly.Name} {_assembly.Version}");
            ConsolePresentation.WriteLabel("Location", _assembly.FilePath ?? "(none)");
            ConsolePresentation.WriteLabel("Types", _assembly.ClassCount.ToString(CultureInfo.InvariantCulture));
        }
        catch (OperationCanceledException)
        {
            ConsolePresentation.WriteWarning("Refresh cancelled.");
        }
        catch (Exception exception)
        {
            await logger.LogErrorAsync($"{LogEvents.MetadataAnalysisFailed}: {exception.Message}")
                .ConfigureAwait(false);

            ConsolePresentation.WriteError($"Refresh failed: {exception.Message}");
        }
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!TryGetAssembly(out var assembly))
        {
            return;
        }

        try
        {
            await metadataService.SaveAsync(assembly, cancellationToken).ConfigureAwait(false);
            ConsolePresentation.WriteSuccess($"Analysis saved: {assembly.Name}");
        }
        catch (OperationCanceledException)
        {
            ConsolePresentation.WriteWarning("Saving cancelled.");
        }
        catch (Exception exception)
        {
            await logger.LogErrorAsync($"{LogEvents.MetadataRepositoryFailed}: {exception.Message}")
                .ConfigureAwait(false);

            ConsolePresentation.WriteError($"Saving failed: {exception.Message}");
        }
    }

    private async Task LoadFromRepositoryAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<string> names;

        try
        {
            names = await metadataService.ListAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            ConsolePresentation.WriteWarning("Reading the repository cancelled.");
            return;
        }
        catch (Exception exception)
        {
            ConsolePresentation.WriteError($"Reading the repository failed: {Describe(exception)}");
            return;
        }

        if (names.Count == 0)
        {
            ConsolePresentation.WriteWarning("The repository contains no analyses.");
            return;
        }

        ConsolePresentation.WriteTable(names
            .Select((name, index) => ($"[{index + 1}]", name))
            .ToList());

        if (!TryReadSelection("Select analysis: ", names.Count, out var index))
        {
            return;
        }

        try
        {
            var assembly = await metadataService.LoadAsync(names[index], cancellationToken).ConfigureAwait(false);

            if (assembly is null)
            {
                ConsolePresentation.WriteWarning($"Analysis '{names[index]}' was not found.");
                return;
            }

            _assembly = assembly;
            ConsolePresentation.WriteSuccess($"Analysis loaded: {assembly.Name} {assembly.Version}");
        }
        catch (OperationCanceledException)
        {
            ConsolePresentation.WriteWarning("Reading the analysis cancelled.");
        }
        catch (Exception exception)
        {
            ConsolePresentation.WriteError($"Reading the analysis failed: {Describe(exception)}");
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

    private static string Describe(Exception exception) =>
        exception is MetadataServiceException { UserMessage: var message } ? message : exception.Message;

    private async Task HandleInvalidOptionAsync(string choice)
    {
        ConsolePresentation.WriteWarning($"Invalid option '{choice}'. Choose one of: 1, 2, 3, 4, 5, 6, 7, 8, 0.");
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
