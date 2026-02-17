// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using GenerativeAI;
using NaturalLanguageGenerator;
using Shared;
using CSharpGenerator.Models;
using CSharpGenerator.Generators;
using static CSharpGenerator.Generators.FrontmatterUtility;
using TemplateEngine;

namespace CSharpGenerator;

/// <summary>
/// Handles all documentation generation logic, including data transformation,
/// page generation, and common parameter analysis.
/// </summary>
public static class DocumentationGenerator
{
    private static readonly Regex ConditionalRequirementRegex = new(
        @"Requires at least one[^.]*\.?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ParameterNameRegex = new(
        @"--[A-Za-z0-9-]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Generates comprehensive documentation from CLI output data.
    /// </summary>
    public static async Task<int> GenerateAsync(
        string cliOutputFile,
        string outputDir,
        bool generateToolPages = false,
        bool generateIndex = false,
        bool generateCommon = false,
        bool generateCommands = false,
        bool generateAnnotations = false,
        string? cliVersion = null,
        bool generateExamplePrompts = false,
        bool generateCompleteTools = false,
        bool validatePrompts = false)
    {
        // Config.Load has been called in Program.Main and TextCleanup is initialized statically

        // Validate input files exist
        if (!File.Exists(cliOutputFile))
        {
            Console.Error.WriteLine($"Error: CLI output file not found: {cliOutputFile}");
            return 1;
        }

        // Validate output directory exists or can be created
        if (!Directory.Exists(outputDir))
        {
            try
            {
                Directory.CreateDirectory(outputDir);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: Cannot create output directory '{outputDir}': {ex.Message}");
                return 1;
            }
        }

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
        
        // Set version if provided
        if (!string.IsNullOrWhiteSpace(cliVersion))
        {
            transformedData.Version = cliVersion;
            LogFileHelper.WriteDebug($"Using CLI version: {cliVersion}");
        }

        // Load common parameters from JSON file
        transformedData.SourceDiscoveredCommonParams = await LoadCommonParametersFromFile();

        // Ensure output directory exists
        Directory.CreateDirectory(outputDir);
        
        // Use output directory as the base for sibling folders (annotations, parameters, etc.)
        var parentDir = outputDir;
        
        // Create common-general directory for general documentation files
        var commonGeneralDir = Path.Combine(parentDir, "common-general");
        Directory.CreateDirectory(commonGeneralDir);

        // Setup templates directory (relative to the project directory)
        var templatesDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "templates");
        
        // Generate individual annotation files for each tool first (at parent level)
        // This way we can reference them in the area pages
        var annotationsDir = Path.Combine(parentDir, "annotations");
        Directory.CreateDirectory(annotationsDir);
        var annotationTemplate = Path.Combine(templatesDir, "annotation-template.hbs");
        
        // Initialize all generators with shared dependencies
        var annotationGenerator = new AnnotationGenerator();
        var parameterGenerator = new ParameterGenerator();
        var pageGenerator = new PageGenerator(ExtractCommonParameters);

        // Generate annotation files
        await annotationGenerator.GenerateAnnotationFilesAsync(transformedData, annotationsDir, annotationTemplate);

        // Generate individual parameter files for each tool (at parent level)
        var parametersDir = Path.Combine(parentDir, "parameters");
        Directory.CreateDirectory(parametersDir);
        var parameterTemplate = Path.Combine(templatesDir, "parameter-template.hbs");
        await parameterGenerator.GenerateParameterFilesAsync(transformedData, parametersDir, parameterTemplate);

        // Setup area template (needed for index page too)
        var areaTemplate = Path.Combine(templatesDir, "area-template.hbs");

        // Generate common tools page if requested
        if (generateCommon)
        {
            var commonTemplate = Path.Combine(templatesDir, "common-tools.hbs");
            await pageGenerator.GenerateCommonToolsPageAsync(transformedData, outputDir, commonTemplate);
        }

        // Generate index page if requested
        if (generateIndex)
        {
            await pageGenerator.GenerateIndexPageAsync(transformedData, outputDir, areaTemplate);
        }

        // Generate commands page if requested
        if (generateCommands)
        {
            var commandsTemplate = Path.Combine(templatesDir, "tool-family-page.hbs");
            await pageGenerator.GenerateCommandsPageAsync(transformedData, outputDir, commandsTemplate);
        }
        
        // Generate tool annotations summary file if annotations are enabled (but not in annotations-only mode)
        if (generateAnnotations && (generateCommands || generateIndex || generateCommon))
        {
            var toolAnnotationsTemplate = Path.Combine(templatesDir, "tool-annotations-template.hbs");
            var generatedDir = string.Equals(Path.GetFileName(outputDir), "generated", StringComparison.OrdinalIgnoreCase)
                ? outputDir
                : (Path.GetDirectoryName(outputDir) ?? outputDir);
            await annotationGenerator.GenerateToolAnnotationsSummaryAsync(transformedData, generatedDir, toolAnnotationsTemplate, annotationsDir);
        }

        // Note: Annotations are always generated at the start, before area pages

        // Output summary statistics
        var totalTools = transformedData.Tools.Count;
        var totalAreas = transformedData.Areas.Count;
        
        Console.WriteLine($"Generation Summary:");
        Console.WriteLine($"  Total tools: {totalTools}");
        Console.WriteLine($"  Total service areas: {totalAreas}");
        
        // Show security summary
        var secretsCount = transformedData.Tools.Count(t => t.Metadata?.Secret?.Value == true);
        var consentCount = transformedData.Tools.Count(t => t.Metadata?.LocalRequired?.Value == true);
        Console.WriteLine($"  Security requirements:");
        Console.WriteLine($"    Tools requiring secrets: {secretsCount}");
        Console.WriteLine($"    Tools requiring local consent: {consentCount}");
        
        // Get common parameter names for filtering
        var commonParameters = transformedData.SourceDiscoveredCommonParams.Any() 
            ? transformedData.SourceDiscoveredCommonParams 
            : ExtractCommonParameters(transformedData.Tools);
        var commonParameterNames = new HashSet<string>(commonParameters.Select(p => p.Name ?? ""));
        
        // Log detailed area breakdown to file
        foreach (var area in transformedData.Areas.OrderBy(a => a.Key))
        {
            var totalParams = area.Value.Tools.Sum(t => t.Option?.Count ?? 0);
            LogFileHelper.WriteDebug($"  {area.Key}: {area.Value.ToolCount} tools ({totalParams} parameters)");
        }
        
        // Log detailed tool list to file, not console
        LogFileHelper.WriteDebug("");
        LogFileHelper.WriteDebug("Tool List by Service Area:");
        LogFileHelper.WriteDebug("=" + new string('=', 29));
        LogFileHelper.WriteDebug("");
        LogFileHelper.WriteDebug("Legend: [A] = Annotation file, [P] = Parameter file, [E] = Example prompts file");
        LogFileHelper.WriteDebug("");
        
        foreach (var area in transformedData.Areas.OrderBy(a => a.Key))
        {
            LogFileHelper.WriteDebug("");
            LogFileHelper.WriteDebug($"{area.Key} ({area.Value.ToolCount} tools):");
            LogFileHelper.WriteDebug(new string('-', area.Key.Length + $" ({area.Value.ToolCount} tools):".Length));
            
            foreach (var tool in area.Value.Tools.OrderBy(t => t.Command))
            {
                // Calculate non-common parameter count (matches what's shown in parameter tables)
                var nonCommonParamCount = tool.Option?
                    .Count(opt => !string.IsNullOrEmpty(opt.Name) && !commonParameterNames.Contains(opt.Name)) ?? 0;
                
                // Build indicators for generated files
                var indicators = new List<string>();
                if (tool.HasAnnotation) indicators.Add("A");
                if (tool.HasParameters) indicators.Add("P");
                if (tool.HasExamplePrompts) indicators.Add("E");
                var indicatorStr = indicators.Count > 0 ? $" [{string.Join(",", indicators)}]" : "";
                
                LogFileHelper.WriteDebug($"  • {tool.Command,-50} - {tool.Name,-20} [{nonCommonParamCount,2} params]{indicatorStr}");
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
                    LogFileHelper.WriteDebug($"Tool has empty command, skipping.");
                    continue;
                }
            
                var commandParts = tool.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                LogFileHelper.WriteDebug($"Processing command: {tool.Command}, parts: {commandParts.Length}");
                
                if (commandParts.Length < 1)
                {
                    LogFileHelper.WriteDebug($"Command '{tool.Command}' has no parts, skipping.");
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

                // Sort parameters once: required first, then alphabetical by name.
                // All downstream generators inherit this order.
                if (tool.Option != null)
                {
                    tool.Option = ParameterSorting.SortByRequiredThenName(tool.Option).ToList();
                }

                var conditionalRequirement = ExtractConditionalRequirement(tool.Description ?? "");
                if (conditionalRequirement.Parameters.Count > 0)
                {
                    tool.ConditionalRequiredNote = conditionalRequirement.Note;
                    tool.ConditionalRequiredParameters = conditionalRequirement.Parameters;
                    tool.HasConditionalRequired = true;
                }
            }
            catch (Exception ex)
            {
                LogFileHelper.WriteDebug($"Error processing tool command '{tool.Command}': {ex.Message}");
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

        // Generate in common-general directory
        var commonGeneralDir = Path.Combine(Path.GetDirectoryName(outputDir) ?? outputDir, "common-general");
        var outputFile = Path.Combine(commonGeneralDir, "common-tools.md");
        await File.WriteAllTextAsync(outputFile, result);
        Console.WriteLine($"Generated common tools page: common-general/common-tools.md");
    }

    private static (string? Note, List<string> Parameters) ExtractConditionalRequirement(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return (null, new List<string>());
        }

        var match = ConditionalRequirementRegex.Match(description);
        if (!match.Success)
        {
            return (null, new List<string>());
        }

        var note = match.Value.Trim();
        var parameters = ParameterNameRegex.Matches(note)
            .Select(m => m.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (note, parameters);
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

        // Generate in common-general directory
        var commonGeneralDir = Path.Combine(Path.GetDirectoryName(outputDir) ?? outputDir, "common-general");
        var outputFile = Path.Combine(commonGeneralDir, "index.md");
        await File.WriteAllTextAsync(outputFile, result);
        Console.WriteLine($"Generated index page: common-general/index.md");
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

        // Generate in common-general directory
        var commonGeneralDir = Path.Combine(Path.GetDirectoryName(outputDir) ?? outputDir, "common-general");
        var outputFile = Path.Combine(commonGeneralDir, "azmcp-commands.md");
        await File.WriteAllTextAsync(outputFile, result);
        Console.WriteLine($"Generated commands page: common-general/azmcp-commands.md");
    }

    /// <summary>
    /// Loads common parameters from JSON configuration file.
    /// </summary>
    private static async Task<List<CommonParameter>> LoadCommonParametersFromFile()
    {
        var commonParams = await DataFileLoader.LoadCommonParametersAsync();

        if (commonParams.Count == 0)
        {
            return new List<CommonParameter>();
        }

        return commonParams.Select(p => new CommonParameter
        {
            Name = p.Name,
            Type = p.Type,
            IsRequired = p.IsRequired,
            Description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(p.Description)),
            UsagePercent = 100,
            IsHidden = false,
            Source = "common-parameters.json",
            RequiredText = p.IsRequired ? "Required" : "Optional",
            NL_Name = TextCleanup.NormalizeParameter(p.Name)
        }).ToList();
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
    /// Generates a single file with all tool operations and their annotations.
    /// </summary>
    private static async Task GenerateToolAnnotationsSummaryAsync(TransformedData data, string outputDir, string templateFile, string annotationsDir)
    {
        try
        {
            // Collect all tools from all areas
            var allTools = new List<object>();
            
            foreach (var area in data.Areas)
            {
                foreach (var tool in area.Value.Tools)
                {
                    // Read the annotation file for this tool
                    var annotationFileName = $"{tool.Command?.Replace(" ", "-").ToLowerInvariant() ?? "unknown"}-annotations.md";
                    var annotationFilePath = Path.Combine(annotationsDir, annotationFileName);
                    
                    string? annotationContent = null;
                    if (File.Exists(annotationFilePath))
                    {
                        annotationContent = await File.ReadAllTextAsync(annotationFilePath);
                    }
                    
                    allTools.Add(new
                    {
                        command = tool.Command ?? "Unknown",
                        description = tool.Description,
                        area = area.Key,
                        annotationContent = annotationContent,
                        annotationFileName = annotationFileName
                    });
                }
            }

            var pageData = new Dictionary<string, object>
            {
                ["version"] = data.Version,
                ["generatedAt"] = data.GeneratedAt,
                ["tools"] = allTools,
                ["toolCount"] = allTools.Count
            };

            var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, pageData);

            var outputFile = Path.Combine(outputDir, "tool-annotations.md");
            await File.WriteAllTextAsync(outputFile, result);
            Console.WriteLine($"Generated tool annotations summary: tool-annotations.md (in {outputDir})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating tool annotations summary: {ex.Message}");
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
            LogFileHelper.WriteDebug("ParseCommand: Empty command detected");
            return ("", "");
        }
            
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        LogFileHelper.WriteDebug($"ParseCommand: Command '{command}' split into {parts.Length} parts");
        
        if (parts.Length < 2) // Need at least "area operation"
        {
            LogFileHelper.WriteDebug($"ParseCommand: Not enough parts in command '{command}', expected at least 2, got {parts.Length}");
            return ("", "");
        }
            
        try
        {
            if (parts.Length == 2)
            {
                // Format: "area operation"
                LogFileHelper.WriteDebug($"ParseCommand: Command '{command}' parsed as tool family '{parts[0]}' and operation '{parts[1]}'");
                return (parts[0], parts[1]);
            }
            else if (parts.Length >= 3)
            {
                // Format: "area subarea operation" or longer
                // Take everything except the last part as tool family
                var operation = parts.Last();
                var toolFamily = string.Join(" ", parts.Take(parts.Length - 1));
                LogFileHelper.WriteDebug($"ParseCommand: Command '{command}' parsed as tool family '{toolFamily}' and operation '{operation}'");
                return (toolFamily, operation);
            }
            
            LogFileHelper.WriteDebug($"ParseCommand: Unexpected command format: '{command}'");
            return ("", "");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing command '{command}': {ex.Message}");
            return ("", "");
        }
    }

    /// <summary>
    /// Converts a camelCase property name to Title Case with spaces.
    /// Example: "openWorld" -> "Open World", "readOnly" -> "Read Only"
    /// </summary>
    private static string ConvertCamelCaseToTitleCase(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return propertyName;

        var result = new System.Text.StringBuilder();
        result.Append(char.ToUpper(propertyName[0]));

        for (int i = 1; i < propertyName.Length; i++)
        {
            if (char.IsUpper(propertyName[i]))
            {
                result.Append(' ');
            }
            result.Append(propertyName[i]);
        }

        return result.ToString();
    }

    /// <summary>
    /// Generates a report of areas that don't have brand mappings or compound word definitions.
    /// </summary>
    private static async Task GenerateMissingMappingsReportAsync(Dictionary<string, List<string>> missingMappings, string outputDir)
    {
        var parentDir = Path.GetDirectoryName(outputDir) ?? outputDir;
        var reportPath = Path.Combine(parentDir, "missing-word-choice.md");
        
        var report = new StringBuilder();
        report.AppendLine("# Missing Brand Mappings and Compound Words");
        report.AppendLine();
        report.AppendLine("This report lists MCP server areas that don't have entries in `brand-to-server-mapping.json` or `compound-words.json`.");
        report.AppendLine();
        report.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine();
        
        // Section 1: Unique missing areas
        report.AppendLine("## Missing Areas");
        report.AppendLine();
        report.AppendLine("These areas need to be added to either:");
        report.AppendLine("- `brand-to-server-mapping.json` - for proper brand names and filenames");
        report.AppendLine("- `compound-words.json` - for word separation (e.g., `nodepool` → `node-pool`)");
        report.AppendLine();
        
        var sortedAreas = missingMappings.Keys.OrderBy(k => k).ToList();
        foreach (var area in sortedAreas)
        {
            var toolCount = missingMappings[area].Count;
            report.AppendLine($"- **{area}** ({toolCount} tool{(toolCount > 1 ? "s" : "")})");
        }
        
        report.AppendLine();
        report.AppendLine("## Tools by Missing Area");
        report.AppendLine();
        report.AppendLine("Complete list of tools affected by each missing area:");
        report.AppendLine();
        
        // Section 2: Tools grouped by area
        foreach (var area in sortedAreas)
        {
            report.AppendLine($"### {area}");
            report.AppendLine();
            
            var tools = missingMappings[area].OrderBy(t => t).ToList();
            foreach (var tool in tools)
            {
                report.AppendLine($"- `{tool}`");
            }
            
            report.AppendLine();
        }
        
        // Section 3: Recommendations
        report.AppendLine("## Recommendations");
        report.AppendLine();
        report.AppendLine("### For brand-to-server-mapping.json");
        report.AppendLine();
        report.AppendLine("Add entries like this:");
        report.AppendLine("```json");
        report.AppendLine("{");
        report.AppendLine("  \"brandName\": \"Azure <Service Name>\",");
        report.AppendLine("  \"mcpServerName\": \"<area>\",");
        report.AppendLine("  \"shortName\": \"<Short Name>\",");
        report.AppendLine("  \"fileName\": \"azure-<service-name>\"");
        report.AppendLine("}");
        report.AppendLine("```");
        report.AppendLine();
        
        report.AppendLine("### For compound-words.json");
        report.AppendLine();
        report.AppendLine("Add entries like this:");
        report.AppendLine("```json");
        report.AppendLine("{");
        report.AppendLine("  \"nodepool\": \"node-pool\",");
        report.AppendLine("  \"activitylog\": \"activity-log\"");
        report.AppendLine("}");
        report.AppendLine("```");
        
        await File.WriteAllTextAsync(reportPath, report.ToString());
        Console.WriteLine($"\n📋 Generated missing mappings report: {reportPath}");
        Console.WriteLine($"   Found {missingMappings.Count} area(s) without brand mapping or compound word definition");
    }

    /// <summary>
    /// Validates generated example prompts using LLM with full tool context.
    /// Reads complete tool files from generated/tools/ directory for rich context.
    /// </summary>
    private static async Task ValidateExamplePromptsAsync(TransformedData data, string examplePromptsDir, string toolsDir)
    {
        if (!Directory.Exists(examplePromptsDir))
        {
            Console.WriteLine($"⚠️  Example prompts directory not found: {examplePromptsDir}");
            return;
        }

        if (!Directory.Exists(toolsDir))
        {
            Console.WriteLine($"⚠️  Tools directory not found: {toolsDir}");
            Console.WriteLine($"   Validation requires --complete-tools flag to generate tool files");
            return;
        }

        // Initialize LLM validator
        var validator = new ExamplePromptValidator.PromptValidator();
        if (!validator.IsInitialized())
        {
            Console.WriteLine($"⚠️  Validator not initialized. Check Azure OpenAI configuration.");
            return;
        }

        var validationResults = new List<(string toolCommand, ExamplePromptValidator.ValidationResult? result)>();
        int totalTools = 0;
        int toolsWithPrompts = 0;
        int validTools = 0;
        int invalidTools = 0;
        int skippedTools = 0;

        foreach (var tool in data.Tools)
        {
            totalTools++;
            
            if (!tool.HasExamplePrompts || tool.AnnotationFileName == null)
            {
                continue;
            }

            toolsWithPrompts++;
            
            // Derive filenames
            var exampleFileName = tool.AnnotationFileName.Replace("-annotations.md", "-example-prompts.md");
            var exampleFilePath = Path.Combine(examplePromptsDir, exampleFileName);
            
            // Find the complete tool file
            var toolFileName = tool.AnnotationFileName.Replace("-annotations.md", ".complete.md");
            var toolFilePath = Path.Combine(toolsDir, toolFileName);

            if (!File.Exists(exampleFilePath))
            {
                Console.WriteLine($"  ⚠️  {tool.Command,-50} (example prompts file not found)");
                skippedTools++;
                continue;
            }

            if (!File.Exists(toolFilePath))
            {
                Console.WriteLine($"  ⚠️  {tool.Command,-50} (complete tool file not found)");
                skippedTools++;
                continue;
            }

            // Read complete tool file
            var toolFileContent = await File.ReadAllTextAsync(toolFilePath);

            // Validate using LLM - just pass the complete tool file
            var result = await validator.ValidateWithLLMAsync(toolFileContent);

            if (result == null)
            {
                Console.WriteLine($"  ⚠️  {tool.Command,-50} (validation failed)");
                skippedTools++;
                continue;
            }

            validationResults.Add((tool.Command ?? "unknown", result));

            if (result.IsValid)
            {
                validTools++;
                Console.WriteLine($"  ✅ {tool.Command,-50} ({result.TotalPrompts} prompts, all valid)");
            }
            else
            {
                invalidTools++;
                var missingParams = string.Join(", ", result.Validation
                    .SelectMany(v => v.MissingParameters)
                    .Distinct());
                Console.WriteLine($"  ❌ {tool.Command,-50} ({result.InvalidPrompts}/{result.TotalPrompts} prompts invalid)");
            }
        }

        // Print summary
        Console.WriteLine("\n=== Validation Summary ===");
        Console.WriteLine($"Total tools: {totalTools}");
        Console.WriteLine($"Tools with example prompts: {toolsWithPrompts}");
        Console.WriteLine($"Validated: {validTools + invalidTools}");
        Console.WriteLine($"Valid tools: {validTools} ({(toolsWithPrompts > 0 ? (validTools * 100.0 / toolsWithPrompts).ToString("F1") : "0.0")}%)");
        Console.WriteLine($"Invalid tools: {invalidTools} ({(toolsWithPrompts > 0 ? (invalidTools * 100.0 / toolsWithPrompts).ToString("F1") : "0.0")}%)");
        Console.WriteLine($"Skipped: {skippedTools}");

        // Generate validation report
        var parentDir = Path.GetDirectoryName(examplePromptsDir) ?? examplePromptsDir;
        var logsDir = Path.Combine(parentDir, "logs");
        Directory.CreateDirectory(logsDir);
        var reportPath = Path.Combine(logsDir, "example-prompt-validation-report.md");
        
        var report = new System.Text.StringBuilder();
        report.AppendLine("# Example Prompt Validation Report (LLM-Based)");
        report.AppendLine();
        report.AppendLine($"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        report.AppendLine();
        report.AppendLine("## Summary");
        report.AppendLine();
        report.AppendLine($"- **Total tools:** {totalTools}");
        report.AppendLine($"- **Tools with example prompts:** {toolsWithPrompts}");
        report.AppendLine($"- **Validated:** {validTools + invalidTools}");
        report.AppendLine($"- **Valid tools:** {validTools} ({(toolsWithPrompts > 0 ? (validTools * 100.0 / toolsWithPrompts).ToString("F1") : "0.0")}%)");
        report.AppendLine($"- **Invalid tools:** {invalidTools} ({(toolsWithPrompts > 0 ? (invalidTools * 100.0 / toolsWithPrompts).ToString("F1") : "0.0")}%)");
        report.AppendLine($"- **Skipped:** {skippedTools}");
        report.AppendLine();

        if (invalidTools > 0)
        {
            report.AppendLine("## Tools with Issues");
            report.AppendLine();
            
            foreach (var (toolCommand, result) in validationResults.Where(r => r.result != null && !r.result.IsValid))
            {
                if (result == null) continue;
                
                report.AppendLine($"### `{toolCommand}`");
                report.AppendLine();
                report.AppendLine($"**Summary:** {result.Summary}");
                report.AppendLine();
                report.AppendLine($"**Required Parameters:** {string.Join(", ", result.RequiredParameters)}");
                report.AppendLine();
                
                if (result.Recommendations.Any())
                {
                    report.AppendLine("**Recommendations:**");
                    foreach (var rec in result.Recommendations)
                    {
                        report.AppendLine($"- {rec}");
                    }
                    report.AppendLine();
                }

                report.AppendLine("**Prompt Details:**");
                report.AppendLine();
                report.AppendLine("| Prompt | Valid | Issues |");
                report.AppendLine("|--------|-------|--------|");
                foreach (var promptValidation in result.Validation)
                {
                    var status = promptValidation.IsValid ? "✅" : "❌";
                    var issues = promptValidation.Issues.Any() 
                        ? string.Join("; ", promptValidation.Issues)
                        : "-";
                    var promptPreview = promptValidation.Prompt.Length > 60 
                        ? promptValidation.Prompt.Substring(0, 57) + "..."
                        : promptValidation.Prompt;
                    report.AppendLine($"| {promptPreview} | {status} | {issues} |");
                }
                report.AppendLine();
            }
        }

        if (validTools > 0)
        {
            report.AppendLine("## Valid Tools");
            report.AppendLine();
            report.AppendLine($"{validTools} tools have all required parameters in their example prompts:");
            report.AppendLine();
            foreach (var (toolCommand, result) in validationResults.Where(r => r.result != null && r.result.IsValid))
            {
                report.AppendLine($"- `{toolCommand}`");
            }
            report.AppendLine();
        }

        await File.WriteAllTextAsync(reportPath, report.ToString());
        Console.WriteLine($"\n📋 Example prompt validation report saved to: {reportPath}");
    }
}
