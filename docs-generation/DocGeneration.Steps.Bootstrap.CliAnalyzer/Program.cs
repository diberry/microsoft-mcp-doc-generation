using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.CommandLine;
using CliAnalyzer.Analyzers;
using CliAnalyzer.Reports;
using CliAnalyzer.Services;

var rootCommand = new RootCommand("CLI JSON visual analyzer for Azure MCP tools");

// File option
var fileOption = new Option<FileInfo?>(
    ["--file", "-f"],
    "Path to the CLI JSON file to analyze")
{
    IsRequired = false
};

// Output option
var outputOption = new Option<FileInfo?>(
    ["--output", "-o"],
    "Path for Markdown report output (default: cli-analysis-report.md)")
{
    IsRequired = false
};

// Namespace option
var namespaceOption = new Option<string?>(
    ["--namespace", "-n"],
    "Show detailed analysis for a specific namespace")
{
    IsRequired = false
};

// Tool option
var toolOption = new Option<string?>(
    ["--tool", "-t"],
    "Show detailed analysis for a specific tool (use with --namespace)")
{
    IsRequired = false
};

// HTML only option
var htmlOnlyOption = new Option<bool>(
    ["--html-only"],
    "Generate Markdown report only, skip console output")
{
    IsRequired = false
};

rootCommand.AddOption(fileOption);
rootCommand.AddOption(outputOption);
rootCommand.AddOption(namespaceOption);
rootCommand.AddOption(toolOption);
rootCommand.AddOption(htmlOnlyOption);

rootCommand.SetHandler(
    async (file, output, ns, tool, htmlOnly) => await RunAnalysis(file, output, ns, tool, htmlOnly),
    fileOption, outputOption, namespaceOption, toolOption, htmlOnlyOption);

return await rootCommand.InvokeAsync(args);

async Task RunAnalysis(FileInfo? file, FileInfo? output, string? ns, string? tool, bool htmlOnly)
{
    try
    {
        var loader = new CliJsonLoader();
        var analyzer = new CliDataAnalyzer();
        var consoleReporter = new ConsoleReporter();
        var markdownReporter = new MarkdownReporter();

        // Load data
        string cliFilePath = file?.FullName ?? "generated/cli/cli-output.json";

        // If relative path, resolve from current directory or go up to find the generated folder
        if (!Path.IsPathRooted(cliFilePath))
        {
            var currentDir = Directory.GetCurrentDirectory();
            var potentialPath = Path.Combine(currentDir, cliFilePath);
            
            if (!File.Exists(potentialPath))
            {
                // Try going up one level (if running from docs-generation directory)
                var parentPath = Path.Combine(Path.GetDirectoryName(currentDir)!, cliFilePath);
                if (File.Exists(parentPath))
                {
                    potentialPath = parentPath;
                }
            }
            
            cliFilePath = potentialPath;
        }

        Console.WriteLine($"Loading CLI JSON from: {cliFilePath}");
        var cliData = await loader.LoadFromFileAsync(cliFilePath);
        Console.WriteLine($"Loaded {cliData.Results.Count} tools\n");

        // Analyze
        var results = analyzer.Analyze(cliData);

        // Display results
        if (!string.IsNullOrWhiteSpace(ns))
        {
            var namespace_ = results.Namespaces.FirstOrDefault(n => 
                n.Name.Equals(ns, StringComparison.OrdinalIgnoreCase));

            if (namespace_ != null)
            {
                if (!string.IsNullOrWhiteSpace(tool))
                {
                    var toolObj = namespace_.Tools.FirstOrDefault(t => 
                        t.Name.Equals(tool, StringComparison.OrdinalIgnoreCase));

                    if (toolObj != null)
                    {
                        consoleReporter.PrintToolDetail(namespace_.Name, toolObj);
                    }
                    else
                    {
                        Console.Error.WriteLine($"Tool '{tool}' not found in namespace '{ns}'");
                    }
                }
                else
                {
                    consoleReporter.PrintNamespaceDetail(namespace_);
                }
            }
            else
            {
                Console.Error.WriteLine($"Namespace '{ns}' not found");
            }
        }
        else if (!htmlOnly)
        {
            consoleReporter.PrintSummary(results);
        }

        // Generate Markdown report
        string markdownPath = output?.FullName ?? "cli-analysis-report.md";
        if (!Path.IsPathRooted(markdownPath))
        {
            markdownPath = Path.Combine(Directory.GetCurrentDirectory(), markdownPath);
        }

        await markdownReporter.GenerateReportAsync(results, markdownPath);
        Console.WriteLine($"\nMarkdown report generated: {markdownPath}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        if (ex.InnerException != null)
        {
            Console.Error.WriteLine($"Details: {ex.InnerException.Message}");
        }
        Environment.Exit(1);
    }
}
