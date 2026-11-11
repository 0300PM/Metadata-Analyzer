using MetadataAnalyzer.Core.Interfaces;
using MetadataAnalyzer.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace MetadataAnalyzer.CLI;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        var services = new ServiceCollection();
        services.AddMetadataAnalyzer(configuration, logToConsole: false);
        services.AddSingleton<TextReader>(_ => Console.In);
        services.AddSingleton<TextWriter>(_ => Console.Out);
        services.AddSingleton<MetadataConsoleApp>();

        await using var provider = services.BuildServiceProvider();
        var app = provider.GetRequiredService<MetadataConsoleApp>();

        try
        {
            return await app.RunAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine();
            Console.WriteLine("Operation cancelled.");
            return 1;
        }
        catch (Exception exception)
        {
            await provider.GetRequiredService<IAppLogger>()
                .LogErrorAsync($"Unhandled CLI failure: {exception}")
                .ConfigureAwait(false);

            Console.Error.WriteLine($"Unhandled error: {exception.Message}");
            return 1;
        }
    }
}
