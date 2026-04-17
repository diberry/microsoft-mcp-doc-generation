using System.CommandLine;
using System.Text.Json;
using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Models;
using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Services;

namespace DocGeneration.Steps.Bootstrap.ToolMetadataEnricher;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private static async Task<int> Main(string[] args)
    {
        var cliOutputOption = new Option<string>(
            "--cli-output",
            description: "Path to cli-output.json")
        {
            IsRequired = true
        };

        var azmcpCommandsOption = new Option<string>(
            "--azmcp-commands",
            description: "Path to azmcp-commands.json")
        {
            IsRequired = true
        };

        var outputOption = new Option<string>(
            "--output",
            description: "Path to write cli-output-enriched.json")
        {
            IsRequired = true
        };

        var rootCommand = new RootCommand("Enriches npm CLI output with azmcp command metadata")
        {
            cliOutputOption,
            azmcpCommandsOption,
            outputOption
        };

        var handlerExitCode = 0;

        rootCommand.SetHandler(async (string cliOutput, string azmcpCommands, string output) =>
        {
            handlerExitCode = await ExecuteAsync(cliOutput, azmcpCommands, output);
        }, cliOutputOption, azmcpCommandsOption, outputOption);

        var invokeExitCode = await rootCommand.InvokeAsync(args);
        return invokeExitCode != 0 ? invokeExitCode : handlerExitCode;
    }

    private static async Task<int> ExecuteAsync(string cliOutputPath, string azmcpCommandsPath, string outputPath)
    {
        if (!File.Exists(cliOutputPath))
        {
            Console.Error.WriteLine($"File not found: {cliOutputPath}");
            return 1;
        }

        if (!File.Exists(azmcpCommandsPath))
        {
            Console.Error.WriteLine($"File not found: {azmcpCommandsPath}");
            return 1;
        }

        try
        {
            var cliOutputDocument = await DeserializeAsync<CliOutputDocument>(cliOutputPath);
            if (cliOutputDocument is null)
            {
                Console.Error.WriteLine($"Failed to deserialize CLI output JSON: {cliOutputPath}");
                return 1;
            }

            var azmcpCommandsDocument = await DeserializeAsync<AzmcpCommandsDocument>(azmcpCommandsPath);
            if (azmcpCommandsDocument is null)
            {
                Console.Error.WriteLine($"Failed to deserialize azmcp commands JSON: {azmcpCommandsPath}");
                return 1;
            }

            var orchestrator = new EnrichmentOrchestrator(
                new ToolMatcher(azmcpCommandsDocument),
                new ConditionalParamExtractor(),
                new ParameterEnricher(azmcpCommandsDocument.GlobalOptions));

            var enrichedOutput = orchestrator.Enrich(cliOutputDocument);
            var outputFullPath = Path.GetFullPath(outputPath);
            var outputDirectory = Path.GetDirectoryName(outputFullPath);
            if (!string.IsNullOrWhiteSpace(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            var outputJson = JsonSerializer.Serialize(enrichedOutput, JsonOptions);
            await File.WriteAllTextAsync(outputFullPath, outputJson);

            var metadata = enrichedOutput.EnrichmentMetadata;
            Console.WriteLine($"Total tools: {metadata.TotalTools}");
            Console.WriteLine($"Matched tools: {metadata.MatchedTools}");
            Console.WriteLine($"Unmatched tools: {metadata.UnmatchedTools}");
            Console.WriteLine($"Conditional groups found: {metadata.ConditionalGroupsFound}");
            Console.WriteLine($"Output: {outputFullPath}");

            return 0;
        }
        catch (JsonException ex)
        {
            Console.Error.WriteLine($"Failed to parse JSON: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Enrichment failed: {ex.Message}");
            return 1;
        }
    }

    private static async Task<T?> DeserializeAsync<T>(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
