// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text.Json;
using NaturalLanguageGenerator;
using Shared;

/// <summary>
/// Handles all documentation generation logic, including data transformation,
/// page generation, and common parameter analysis.
/// </summary>
public static class DocumentationGenerator
{
    /// <summary>
    /// Generates comprehensive documentation from CLI output data.
    /// </summary>
    public static async Task<int> GenerateAsync(
        string cliOutputFile,
        string outputDir,
        bool generateIndex = false,
        bool generateCommon = false,
        bool generateCommands = false,
        bool generateServiceOptions = true)
    {
        // Config.Load has been called in Program.Main and TextCleanup is initialized statically

        // Read CLI output
        var cliOutputJson = await File.ReadAllTextAsync(cliOutputFile);
        var cliOutput = JsonSerializer.Deserialize<CliOutput>(cliOutputJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (cliOutput?.Results == null)
        {
            Console.Error.WriteLine("Failed to parse CLI output or no results found");
            return 1;
        }

        // Transform CLI output to expected format
        var transformedData = TransformCliOutput(cliOutput);

        // Add source code discovered common parameters
        var sourceCommonParams = await OptionsDiscovery.DiscoverCommonParametersFromSource();

        // Merge source-discovered parameters with CLI-discovered ones
        transformedData = MergeCommonParameters(transformedData, sourceCommonParams);

        // Ensure output directory exists
        Directory.CreateDirectory(outputDir);

        // Generate area pages
        var templatesDir = Path.Combine("..", "templates");
        var areaTemplate = Path.Combine(templatesDir, "area-template.hbs");

        foreach (var area in transformedData.Areas)
        {
            await GenerateAreaPageAsync(area.Key, area.Value, transformedData, outputDir, areaTemplate);
        }

        // Generate common tools page if requested
        if (generateCommon)
        {
            var commonTemplate = Path.Combine(templatesDir, "common-tools.hbs");
            await GenerateCommonToolsPageAsync(transformedData, outputDir, commonTemplate);
        }

        // Generate index page if requested
        if (generateIndex)
        {
            await GenerateIndexPageAsync(transformedData, outputDir, areaTemplate);
        }

        // Generate commands page if requested
        if (generateCommands)
        {
            var commandsTemplate = Path.Combine(templatesDir, "commands-template.hbs");
            await GenerateCommandsPageAsync(transformedData, outputDir, commandsTemplate);
        }
        
        // Generate service options page
        if (generateServiceOptions)
        {
            var serviceOptionsTemplate = Path.Combine(templatesDir, "service-start-option.hbs");
            await GenerateServiceOptionsPageAsync(transformedData, outputDir, serviceOptionsTemplate);
        }

        // Output summary statistics
        var totalTools = transformedData.Tools.Count;
        var totalAreas = transformedData.Areas.Count;
        
        Console.WriteLine($"Generation Summary:");
        Console.WriteLine($"  Total tools: {totalTools}");
        Console.WriteLine($"  Total service areas: {totalAreas}");
        
        foreach (var area in transformedData.Areas.OrderBy(a => a.Key))
        {
            Console.WriteLine($"  {area.Key}: {area.Value.ToolCount} tools");
        }
        
        Console.WriteLine();
        Console.WriteLine("Tool List by Service Area:");
        Console.WriteLine("=" + new string('=', 29));
        
        foreach (var area in transformedData.Areas.OrderBy(a => a.Key))
        {
            Console.WriteLine();
            Console.WriteLine($"{area.Key} ({area.Value.ToolCount} tools):");
            Console.WriteLine(new string('-', area.Key.Length + $" ({area.Value.ToolCount} tools):".Length));
            
            foreach (var tool in area.Value.Tools.OrderBy(t => t.Command))
            {
                Console.WriteLine($"  • {tool.Command} - {tool.Name}");
            }
        }

        return 0;
    }

    /// <summary>
    /// Transforms CLI output into the expected documentation format.
    /// </summary>
    private static TransformedData TransformCliOutput(CliOutput cliOutput)
    {
        var tools = cliOutput.Results;
        var areaGroups = new Dictionary<string, AreaData>();

        foreach (var tool in tools)
        {
            try
            {
                if (string.IsNullOrEmpty(tool.Command))
                {
                    Console.WriteLine($"Warning: Tool has empty command, skipping.");
                    continue;
                }
            
                var commandParts = tool.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Console.WriteLine($"Debug: Processing command: {tool.Command}, parts: {commandParts.Length}");
                
                if (commandParts.Length < 1)
                {
                    Console.WriteLine($"Warning: Command '{tool.Command}' has no parts, skipping.");
                    continue;
                }
                
                var area = commandParts[0]; // e.g., "acr registry list" -> "acr", "storage account list" -> "storage"
                
                if (!areaGroups.ContainsKey(area))
                {
                    areaGroups[area] = new AreaData
                    {
                        Description = $"{area} area tools",
                        ToolCount = 0,
                        Tools = new List<Tool>()
                    };
                }
                
                areaGroups[area].ToolCount++;
                areaGroups[area].Tools.Add(tool);
                
                // Add area property to tool for compatibility
                tool.Area = area;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing tool command '{tool.Command}': {ex.Message}");
            }
        }

        return new TransformedData
        {
            Version = "1.0.0",
            Tools = tools ?? new List<Tool>(),
            Areas = areaGroups,
            GeneratedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Generates documentation page for a specific area.
    /// </summary>
    private static async Task GenerateAreaPageAsync(string areaName, AreaData areaData, TransformedData data, string outputDir, string templateFile)
    {
        try
        {
            var areaNameForFile = areaName.ToLowerInvariant().Replace(" ", "-");
            var fileName = $"{areaNameForFile}.md";
            var outputFile = Path.Combine(outputDir, fileName);

            // Get common parameter names to filter them out - use source-discovered if available
            var commonParameters = data.SourceDiscoveredCommonParams.Any() 
                ? data.SourceDiscoveredCommonParams 
                : ExtractCommonParameters(data.Tools);
            var commonParameterNames = new HashSet<string>(commonParameters.Select(p => p.Name ?? ""));

            // Filter out common parameters from tools for area pages
            var toolsWithFilteredParams = areaData.Tools.Select(tool => 
            {
                var filteredTool = new Tool
                {
                    Name = tool.Name,
                    Command = tool.Command,
                    Description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(tool.Description ?? "")),
                    SourceFile = tool.SourceFile,
                    Area = tool.Area
                };
                
                if (tool.Option != null)
                {
                    filteredTool.Option = tool.Option
                        .Where(opt => !string.IsNullOrEmpty(opt.Name) && !commonParameterNames.Contains(opt.Name))
                        .Select(opt => new Option
                        {
                            // Handle CLI-style parameter names
                            Name = opt.Name,
                            // Generate natural language name from parameter name
                            NL_Name = TextCleanup.NormalizeParameter(opt.Name ?? ""),
                            Type = opt.Type,
                            Required = opt.Required,
                            RequiredText = opt.Required == true ? "Required" : "Optional",
                            Description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(opt.Description ?? "")),
                        })
                        .ToList();
                }
                
                return filteredTool;
            }).ToList();

            var areaPageData = new Dictionary<string, object>
            {
                ["areaName"] = areaName,
                ["areaData"] = areaData,
                ["tools"] = toolsWithFilteredParams,
                ["version"] = data.Version,
                ["generatedAt"] = data.GeneratedAt,
                ["generateAreaPage"] = true
            };

            var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, areaPageData);

            await File.WriteAllTextAsync(outputFile, result);
            Console.WriteLine($"Generated area page: {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating area page for {areaName}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    /// <summary>
    /// Generates the common tools documentation page.
    /// </summary>
    private static async Task GenerateCommonToolsPageAsync(TransformedData data, string outputDir, string templateFile)
    {
        // Use source-discovered parameters if available, otherwise fall back to CLI-discovered
        var commonParameters = data.SourceDiscoveredCommonParams.Any() 
            ? data.SourceDiscoveredCommonParams 
            : ExtractCommonParameters(data.Tools);
        
        var commonPageData = new Dictionary<string, object>
        {
            ["version"] = data.Version,
            ["generatedAt"] = data.GeneratedAt,
            ["commonParameters"] = commonParameters
        };

        var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, commonPageData);

        var outputFile = Path.Combine(outputDir, "common-tools.md");
        await File.WriteAllTextAsync(outputFile, result);
        Console.WriteLine($"Generated common tools page: common-tools.md");
    }

    /// <summary>
    /// Generates the index documentation page.
    /// </summary>
    private static async Task GenerateIndexPageAsync(TransformedData data, string outputDir, string templateFile)
    {
        var indexPageData = new Dictionary<string, object>
        {
            ["version"] = data.Version,
            ["generatedAt"] = data.GeneratedAt,
            ["tools"] = data.Tools,
            ["areas"] = data.Areas,
            ["generateIndex"] = true
        };

        var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, indexPageData);

        var outputFile = Path.Combine(outputDir, "index.md");
        await File.WriteAllTextAsync(outputFile, result);
        Console.WriteLine($"Generated index page: index.md");
    }

    /// <summary>
    /// Generates the commands documentation page.
    /// </summary>
    private static async Task GenerateCommandsPageAsync(TransformedData data, string outputDir, string templateFile)
    {
        // Use source-discovered parameters if available, otherwise fall back to CLI-discovered
        var commonParameters = data.SourceDiscoveredCommonParams.Any() 
            ? data.SourceDiscoveredCommonParams 
            : ExtractCommonParameters(data.Tools);
        
        var commandsPageData = new Dictionary<string, object>
        {
            ["version"] = data.Version,
            ["generatedAt"] = data.GeneratedAt,
            ["tools"] = data.Tools,
            ["areas"] = data.Areas,
            ["commonParameters"] = commonParameters
        };

        var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, commandsPageData);

        var outputFile = Path.Combine(outputDir, "azmcp-commands.md");
        await File.WriteAllTextAsync(outputFile, result);
        Console.WriteLine($"Generated commands page: azmcp-commands.md");
    }

    /// <summary>
    /// Extracts common parameters from a collection of tools based on usage frequency.
    /// </summary>
    private static List<CommonParameter> ExtractCommonParameters(List<Tool> tools)
    {
        var parameterCounts = new Dictionary<string, int>();
        var parameterDetails = new Dictionary<string, Option>();
        var totalTools = tools.Count;

        // Analyze all tools to find common parameters
        foreach (var tool in tools)
        {
            if (tool.Option != null)
            {
                foreach (var param in tool.Option)
                {
                    var paramName = param.Name ?? "";
                    if (!parameterCounts.ContainsKey(paramName))
                    {
                        parameterCounts[paramName] = 0;
                        parameterDetails[paramName] = param;
                    }
                    parameterCounts[paramName]++;
                }
            }
        }

        var commonParameters = new List<CommonParameter>();

        // Add parameters that appear in at least 50% of tools
        var threshold = Math.Floor(totalTools * 0.5);
        var commonFromTools = parameterCounts.Where(p => p.Value >= threshold).OrderByDescending(p => p.Value);

        foreach (var param in commonFromTools)
        {
            var paramDetail = parameterDetails[param.Key];
            commonParameters.Add(new CommonParameter
            {
                Name = param.Key,
                Type = paramDetail.Type ?? "string",
                IsRequired = paramDetail.Required == true,
                Description = paramDetail.Description ?? "",
                UsagePercent = Math.Round((param.Value / (double)totalTools) * 100, 1)
            });
        }

        // Sort by usage percentage, then by name
        return commonParameters.OrderByDescending(p => p.UsagePercent).ThenBy(p => p.Name).ToList();
    }

    /// <summary>
    /// Merges CLI-discovered parameters with source-discovered parameters, prioritizing source data.
    /// </summary>
    private static TransformedData MergeCommonParameters(TransformedData data, List<CommonParameter> sourceCommonParams)
    {
        // Get existing common parameters from CLI discovery
        var cliCommonParams = ExtractCommonParameters(data.Tools);
        
        // Create a merged list, prioritizing source-discovered parameters
        var allCommonParams = new Dictionary<string, CommonParameter>();
        
        // Add CLI-discovered parameters first
        foreach (var param in cliCommonParams)
        {
            allCommonParams[param.Name] = param;
        }
        
        // Add/override with source-discovered parameters
        foreach (var param in sourceCommonParams)
        {
            allCommonParams[param.Name] = param;
        }
        
        // Store the merged common parameters
        data.SourceDiscoveredCommonParams = allCommonParams.Values.OrderBy(p => p.Name).ToList();
        
        return data;
    }

    /// <summary>
    /// Generates the service options documentation page.
    /// </summary>
    private static async Task GenerateServiceOptionsPageAsync(TransformedData data, string outputDir, string templateFile)
    {
        try
        {
            // Get service options from source
            var serviceOptions = await ServiceOptionsDiscovery.DiscoverServiceStartOptionsFromSource();
            
            var serviceOptionsPageData = new Dictionary<string, object>
            {
                ["version"] = data.Version,
                ["generatedAt"] = data.GeneratedAt,
                ["serviceOptions"] = serviceOptions
            };

            var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, serviceOptionsPageData);

            var outputFile = Path.Combine(outputDir, "service-start-option.md");
            await File.WriteAllTextAsync(outputFile, result);
            Console.WriteLine($"Generated service options page: service-start-option.md");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating service options page: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    /// <summary>
    /// Parses a command string to extract tool family and operation components.
    /// </summary>
    public static (string toolFamily, string operation) ParseCommand(string command)
    {
        // Expected format: "<area> [<subarea>] <operation>"
        // Examples: 
        // - "aks cluster get" -> ("aks cluster", "get")
        // - "storage account list" -> ("storage account", "list") 
        // - "subscription list" -> ("subscription", "list")
        
        if (string.IsNullOrEmpty(command))
        {
            Console.WriteLine($"Debug: Empty command detected");
            return ("", "");
        }
            
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Console.WriteLine($"Debug: Command '{command}' split into {parts.Length} parts");
        
        if (parts.Length < 2) // Need at least "area operation"
        {
            Console.WriteLine($"Debug: Not enough parts in command '{command}', expected at least 2, got {parts.Length}");
            return ("", "");
        }
            
        try
        {
            if (parts.Length == 2)
            {
                // Format: "area operation"
                Console.WriteLine($"Debug: Command '{command}' parsed as tool family '{parts[0]}' and operation '{parts[1]}'");
                return (parts[0], parts[1]);
            }
            else if (parts.Length >= 3)
            {
                // Format: "area subarea operation" or longer
                // Take everything except the last part as tool family
                var operation = parts.Last();
                var toolFamily = string.Join(" ", parts.Take(parts.Length - 1));
                Console.WriteLine($"Debug: Command '{command}' parsed as tool family '{toolFamily}' and operation '{operation}'");
                return (toolFamily, operation);
            }
            
            Console.WriteLine($"Debug: Unexpected command format: '{command}'");
            return ("", "");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing command '{command}': {ex.Message}");
            return ("", "");
        }
    }
}
