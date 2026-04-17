// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.CommandLine;
using System.CommandLine.Invocation;
using ToolMetadataExtractor.Services;

namespace ToolMetadataExtractor;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Extract tool metadata from MCP tools");
        
        var toolsOption = new Option<string[]>(
            name: "--tools",
            description: "List of tool paths to process (e.g. 'storage account list')")
        {
            AllowMultipleArgumentsPerToken = true,
            IsRequired = false
        };
        
        var toolsFileOption = new Option<FileInfo?>(
            name: "--tools-file",
            description: "Path to a file containing a list of tool paths (one per line)");
        
        var outputFileOption = new Option<FileInfo?>(
            name: "--output",
            description: "Output file for the extracted metadata (JSON format)");
        
        var repoRootOption = new Option<DirectoryInfo>(
            name: "--repo-root",
            description: "Path to the repository root",
            getDefaultValue: () => new DirectoryInfo(GetDefaultRepoRoot()));

        rootCommand.AddOption(toolsOption);
        rootCommand.AddOption(toolsFileOption);
        rootCommand.AddOption(outputFileOption);
        rootCommand.AddOption(repoRootOption);
        
        rootCommand.SetHandler(async (toolsList, toolsFile, outputFile, repoRoot) =>
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
            
            var logger = loggerFactory.CreateLogger<Program>();
            
            try
            {
                // Combine tools from direct arguments and file
                var tools = new List<string>(toolsList);
                
                if (toolsFile != null && toolsFile.Exists)
                {
                    tools.AddRange(await File.ReadAllLinesAsync(toolsFile.FullName));
                }
                
                // Filter out empty items
                tools = tools
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Select(t => t.Trim())
                    .ToList();
                
                if (tools.Count == 0)
                {
                    logger.LogError("No tools specified. Use --tools or --tools-file to specify tools to process");
                }
                else
                {
                    logger.LogInformation("Processing {Count} tools", tools.Count);
                    
                    var service = new MetadataExtractorService(
                        loggerFactory.CreateLogger<MetadataExtractorService>(),
                        repoRoot.FullName);
                    
                    var results = await service.ExtractToolMetadataAsync(tools);
                    
                    var json = JsonConvert.SerializeObject(results, Formatting.Indented);
                    
                    if (outputFile != null)
                    {
                        await File.WriteAllTextAsync(outputFile.FullName, json);
                        logger.LogInformation("Metadata written to {OutputFile}", outputFile.FullName);
                    }
                    else
                    {
                        Console.WriteLine(json);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error extracting tool metadata");
            }
        }, toolsOption, toolsFileOption, outputFileOption, repoRootOption);
        
        return await rootCommand.InvokeAsync(args);
    }
    
    private static string GetDefaultRepoRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dirInfo = new DirectoryInfo(currentDir);
        
        // Navigate up to find repo root (where .git folder exists)
        while (dirInfo != null && !Directory.Exists(Path.Combine(dirInfo.FullName, ".git")))
        {
            dirInfo = dirInfo.Parent;
        }
        
        // If we found a .git folder, return that directory
        if (dirInfo != null && Directory.Exists(Path.Combine(dirInfo.FullName, ".git")))
        {
            return dirInfo.FullName;
        }
        
        // Fallback: try to find it relative to the executing assembly
        var assemblyLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        if (assemblyLocation != null)
        {
            dirInfo = new DirectoryInfo(assemblyLocation);
            
            // Try going up to 5 levels
            for (int i = 0; i < 5; i++)
            {
                if (dirInfo == null)
                    break;
                
                if (Directory.Exists(Path.Combine(dirInfo.FullName, ".git")))
                    return dirInfo.FullName;
                
                dirInfo = dirInfo.Parent;
            }
        }
        
        // Last fallback: return the current directory
        return currentDir;
    }
}