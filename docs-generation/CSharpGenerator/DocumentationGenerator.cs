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

namespace CSharpGenerator;

/// <summary>
/// Handles all documentation generation logic, including data transformation,
/// page generation, and common parameter analysis.
/// </summary>
public static class DocumentationGenerator
{
    private static Dictionary<string, BrandMapping>? _brandMappings;
    private static HashSet<string>? _stopWords;
    private static Dictionary<string, string>? _compoundWords;
    private static readonly Regex ConditionalRequirementRegex = new(
        @"Requires at least one[^.]*\.?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ParameterNameRegex = new(
        @"--[A-Za-z0-9-]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Loads stop words from JSON file.
    /// </summary>
    private static async Task<HashSet<string>> LoadStopWordsAsync()
    {
        if (_stopWords != null)
            return _stopWords;

        var stopWordsFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "stop-words.json");
        var stopWordsJson = await File.ReadAllTextAsync(stopWordsFile);
        var stopWordsList = JsonSerializer.Deserialize<List<string>>(stopWordsJson) ?? new List<string>();
        _stopWords = new HashSet<string>(stopWordsList);
        return _stopWords;
    }

    /// <summary>
    /// Loads compound words mappings from JSON file.
    /// </summary>
    private static async Task<Dictionary<string, string>> LoadCompoundWordsAsync()
    {
        if (_compoundWords != null)
            return _compoundWords;

        var compoundWordsFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "compound-words.json");
        var compoundWordsJson = await File.ReadAllTextAsync(compoundWordsFile);
        _compoundWords = JsonSerializer.Deserialize<Dictionary<string, string>>(compoundWordsJson) ?? new Dictionary<string, string>();
        return _compoundWords;
    }

    /// <summary>
    /// Loads brand-to-server-name mappings from JSON file.
    /// </summary>
    private static async Task<Dictionary<string, BrandMapping>> LoadBrandMappingsAsync()
    {
        if (_brandMappings != null)
            return _brandMappings;

        try
        {
            // Try to resolve the brand mapping relative to the assembly location first (works for dotnet run)
            var candidateFromBin = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "brand-to-server-mapping.json");
            var mappingFile = File.Exists(candidateFromBin)
                ? candidateFromBin
                : Path.Combine("..", "brand-to-server-mapping.json"); // Fallback for legacy invocation paths

            if (!File.Exists(mappingFile))
            {
                Console.WriteLine($"Warning: Brand mapping file not found at {mappingFile}, using default naming");
                return new Dictionary<string, BrandMapping>();
            }

            var json = await File.ReadAllTextAsync(mappingFile);
            var mappings = JsonSerializer.Deserialize<List<BrandMapping>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            _brandMappings = mappings?.ToDictionary(m => m.McpServerName ?? "", m => m) 
                ?? new Dictionary<string, BrandMapping>();
            
            Console.WriteLine($"Loaded {_brandMappings.Count} brand mappings");
            return _brandMappings;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading brand mappings: {ex.Message}");
            return new Dictionary<string, BrandMapping>();
        }
    }

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
        bool generateServiceOptions = true,
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
            Console.WriteLine($"Using CLI version: {cliVersion}");
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
        
        // DEPRECATED: Example prompts generation has been moved to ExamplePromptGeneratorStandalone package
        // Keeping code in place for reference but disabled. Use ExamplePromptGeneratorStandalone instead.
        /*
        // Setup example prompts generation if requested
        ExamplePromptGenerator? examplePromptGenerator = null;
        string? examplePromptsDir = null;
        if (generateExamplePrompts)
        {
            Console.WriteLine("\n=== Example Prompts Generation Requested ===");
            
            // Debug: Check environment variables
            Console.WriteLine("DEBUG: Checking Azure OpenAI environment variables:");
            Console.WriteLine($"  FOUNDRY_API_KEY: {(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FOUNDRY_API_KEY")) ? "NOT SET" : "SET (length: " + Environment.GetEnvironmentVariable("FOUNDRY_API_KEY")?.Length + ")")}");
            Console.WriteLine($"  FOUNDRY_ENDPOINT: {Environment.GetEnvironmentVariable("FOUNDRY_ENDPOINT") ?? "NOT SET"}");
            Console.WriteLine($"  FOUNDRY_MODEL_NAME: {Environment.GetEnvironmentVariable("FOUNDRY_MODEL_NAME") ?? "NOT SET"}");
            
            examplePromptsDir = Path.Combine(parentDir, "example-prompts");
            Directory.CreateDirectory(examplePromptsDir);
            Console.WriteLine($"Example prompts directory: {examplePromptsDir}");
            
            try
            {
                examplePromptGenerator = new ExamplePromptGenerator();
                
                // Verify the generator is functional by checking if OpenAI client is initialized
                if (!examplePromptGenerator.IsInitialized())
                {
                    Console.WriteLine("⚠️  WARNING: ExamplePromptGenerator failed to initialize.");
                    Console.WriteLine("    Azure OpenAI credentials are not configured.");
                    Console.WriteLine("    Required environment variables or .env file entries:");
                    Console.WriteLine("      - FOUNDRY_API_KEY");
                    Console.WriteLine("      - FOUNDRY_ENDPOINT");
                    Console.WriteLine("      - FOUNDRY_MODEL_NAME");
                    Console.WriteLine("    Example prompts will be SKIPPED. Documentation generation will continue.");
                    Console.WriteLine("");
                    examplePromptGenerator = null;
                    examplePromptsDir = null;
                }
                else
                {
                    Console.WriteLine("✅ ExamplePromptGenerator initialized and verified");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️  WARNING: Failed to initialize ExamplePromptGenerator: {ex.Message}");
                Console.WriteLine("    Example prompts will be SKIPPED. Documentation generation will continue.");
                Console.WriteLine("");
                examplePromptGenerator = null;
                examplePromptsDir = null;
            }
            
            Console.WriteLine("=== Starting Annotation Generation (with example prompts) ===\n");
        }
        else
        {
            Console.WriteLine("\n=== Example Prompts Generation NOT Requested ===");
            Console.WriteLine("To enable example prompts generation, use the --example-prompts flag");
            Console.WriteLine("Example: dotnet run --example-prompts\n");
        }
        */
        
        // Disabled: Example prompts generation. Use ExamplePromptGeneratorStandalone package instead.
        object? examplePromptGenerator = null;
        string? examplePromptsDir = null;
        
        // Initialize all generators with shared dependencies
        var annotationGenerator = new AnnotationGenerator(
            LoadBrandMappingsAsync,
            LoadStopWordsAsync,
            LoadCompoundWordsAsync,
            CleanFileNameAsync);
            
        var parameterGenerator = new ParameterGenerator(
            LoadBrandMappingsAsync,
            LoadCompoundWordsAsync,
            CleanFileNameAsync,
            ExtractCommonParameters);
        
        // DEPRECATED: ParamAnnotationGenerator no longer used
        // Keeping variable declaration for backwards compatibility but disabled
        // var paramAnnotationGenerator = new ParamAnnotationGenerator(
        //     LoadBrandMappingsAsync,
        //     LoadCompoundWordsAsync,
        //     CleanFileNameAsync);
            
        var pageGenerator = new PageGenerator(
            LoadBrandMappingsAsync,
            CleanFileNameAsync,
            ExtractCommonParameters);
        
        // DEPRECATED: ToolFamilyPageGenerator no longer used
        // Keeping variable declaration for backwards compatibility but disabled
        // var toolFamilyPageGenerator = new ToolFamilyPageGenerator(
        //     LoadBrandMappingsAsync,
        //     CleanFileNameAsync,
        //     ExtractCommonParameters);
            
        // DEPRECATED: CompleteToolGenerator replaced by ToolGeneration_Composed
        // var completeToolGenerator = new CompleteToolGenerator(
        //     LoadBrandMappingsAsync,
        //     CleanFileNameAsync);
            
        var reportGenerator = new ReportGenerator();

        // Generate annotation files
        await annotationGenerator.GenerateAnnotationFilesAsync(transformedData, annotationsDir, annotationTemplate, examplePromptGenerator, examplePromptsDir);

        // Generate individual parameter files for each tool (at parent level)
        var parametersDir = Path.Combine(parentDir, "parameters");
        Directory.CreateDirectory(parametersDir);
        var parameterTemplate = Path.Combine(templatesDir, "parameter-template.hbs");
        await parameterGenerator.GenerateParameterFilesAsync(transformedData, parametersDir, parameterTemplate);

        // DEPRECATED: Combined parameter and annotation files generation
        // Keeping code in place for reference but disabled.
        // Use separate annotations and parameters files instead (or complete tool files with --complete-tools flag)
        /*
        // Generate combined parameter and annotation files for each tool (at parent level)
        var paramAnnotationDir = Path.Combine(parentDir, "param-and-annotation");
        Directory.CreateDirectory(paramAnnotationDir);
        var paramAnnotationTemplate = Path.Combine(templatesDir, "param-annotation-template.hbs");
        await paramAnnotationGenerator.GenerateParamAnnotationFilesAsync(transformedData, paramAnnotationDir, paramAnnotationTemplate);
        */

        // DEPRECATED: Complete tool files generation replaced by ToolGeneration_Composed
        // Use ToolGeneration_Raw -> ToolGeneration_Composed pipeline instead
        /*
        // Generate complete tool files if requested (at parent level)
        var toolsDir = Path.Combine(parentDir, "tools");
        if (generateCompleteTools)
        {
            Directory.CreateDirectory(toolsDir);
            var completeToolTemplate = Path.Combine(templatesDir, "tool-complete-template.hbs");
            await completeToolGenerator.GenerateCompleteToolFilesAsync(
                transformedData,
                toolsDir,
                completeToolTemplate,
                annotationsDir,
                parametersDir,
                examplePromptsDir ?? Path.Combine(parentDir, "example-prompts"));
        }

        // Validate example prompts if requested (after complete tools are generated)
        if (validatePrompts && generateExamplePrompts && examplePromptsDir != null && generateCompleteTools)
        {
            Console.WriteLine("\n=== Validating Example Prompts with LLM ===");
            await ValidateExamplePromptsAsync(transformedData, examplePromptsDir, toolsDir);
        }
        else if (validatePrompts && (!generateCompleteTools || !generateExamplePrompts))
        {
            Console.WriteLine("\n⚠️  Example prompt validation requires both --complete-tools and --example-prompts flags");
        }
        */

        // Setup area template (needed for index page too)
        var areaTemplate = Path.Combine(templatesDir, "area-template.hbs");
        
        
        // DEPRECATED: Tool pages generation has been moved to ToolGeneration_Raw, ToolGeneration_Composed, and ToolGeneration_Improved packages
        // Keeping code in place for reference but disabled. Use standalone packages instead.
        /*
        // Generate area pages (skip if only generating annotations)
        if (generateToolPages && (!generateAnnotations || generateCommands || generateIndex || generateCommon))
        {
            foreach (var area in transformedData.Areas)
            {
                await toolFamilyPageGenerator.GenerateAsync(area.Key, area.Value, transformedData, outputDir, toolFamilyTemplate);
            }
        }
        */

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
        
        // Generate service options page
        if (generateServiceOptions)
        {
            var serviceOptionsTemplate = Path.Combine(templatesDir, "service-start-option.hbs");
            await pageGenerator.GenerateServiceOptionsPageAsync(transformedData, outputDir, serviceOptionsTemplate);
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

        // Generate security reports in the parent directory (generated folder) (skip if only generating annotations)
        if (!generateAnnotations || generateCommands || generateIndex || generateCommon)
        {
            var securityReportsDir = string.Equals(Path.GetFileName(outputDir), "generated", StringComparison.OrdinalIgnoreCase)
                ? outputDir
                : (Path.GetDirectoryName(outputDir) ?? outputDir);
            await reportGenerator.GenerateSecurityReportsAsync(transformedData, securityReportsDir);
        }

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
        
        foreach (var area in transformedData.Areas.OrderBy(a => a.Key))
        {
            var totalParams = area.Value.Tools.Sum(t => t.Option?.Count ?? 0);
            Console.WriteLine($"  {area.Key}: {area.Value.ToolCount} tools ({totalParams} parameters)");
        }
        
        // Print tool list at the end after all files have been generated
        Console.WriteLine();
        Console.WriteLine("Tool List by Service Area:");
        Console.WriteLine("=" + new string('=', 29));
        Console.WriteLine();
        Console.WriteLine("Legend: [A] = Annotation file, [P] = Parameter file, [E] = Example prompts file");
        Console.WriteLine();
        
        foreach (var area in transformedData.Areas.OrderBy(a => a.Key))
        {
            Console.WriteLine();
            Console.WriteLine($"{area.Key} ({area.Value.ToolCount} tools):");
            Console.WriteLine(new string('-', area.Key.Length + $" ({area.Value.ToolCount} tools):".Length));
            
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
                
                Console.WriteLine($"  • {tool.Command,-50} - {tool.Name,-20} [{nonCommonParamCount,2} params]{indicatorStr}");
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

            // Annotations directory path (at parent level)
            var parentDir = Path.GetDirectoryName(outputDir) ?? outputDir;
            var annotationsDir = Path.Combine(parentDir, "annotations");
            
            // Load brand mappings for annotation filename lookup
            var brandMappings = await LoadBrandMappingsAsync();

            // Filter out common parameters from tools for area pages and add annotation content
            var toolsWithFilteredParamsTasks = areaData.Tools.Select(async tool => 
            {
                var filteredTool = new Tool
                {
                    Name = tool.Name,
                    Command = tool.Command,
                    Description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(tool.Description ?? "")),
                    SourceFile = tool.SourceFile,
                    Area = tool.Area,
                    Metadata = tool.Metadata // Include metadata from CLI output
                };
                
                // Read annotation file content if it exists
                if (!string.IsNullOrEmpty(tool.Command))
                {
                    // Parse command to get brand-based filename
                    var commandParts = tool.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (commandParts.Length > 0)
                    {
                        var area = commandParts[0];
                        
                        // Get brand-based filename from mapping
                        string brandFileName;
                        if (brandMappings.TryGetValue(area, out var mapping) && !string.IsNullOrEmpty(mapping.FileName))
                        {
                            brandFileName = mapping.FileName;
                        }
                        else
                        {
                            brandFileName = area.ToLowerInvariant();
                        }

                        // Build remaining parts
                        var remainingParts = commandParts.Length > 1 
                            ? string.Join("-", commandParts.Skip(1)).ToLowerInvariant()
                            : "";

                        // Clean the filename to match the annotation file generation
                        var cleanedRemainingParts = !string.IsNullOrEmpty(remainingParts) 
                            ? await CleanFileNameAsync(remainingParts) 
                            : "";

                        var annotationFileName = !string.IsNullOrEmpty(cleanedRemainingParts)
                            ? $"{brandFileName}-{cleanedRemainingParts}-annotations.md"
                            : $"{brandFileName}-annotations.md";
                        
                        var annotationFilePath = Path.Combine(annotationsDir, annotationFileName);
                        
                        if (File.Exists(annotationFilePath))
                        {
                            try
                            {
                                filteredTool.AnnotationContent = File.ReadAllText(annotationFilePath);
                                filteredTool.AnnotationFileName = annotationFileName;
                            }
                            catch
                            {
                                // Silently ignore if annotation file can't be read
                                filteredTool.AnnotationContent = "";
                            }
                        }
                    }
                }
                
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
            
            // Add parameter count for template use
            // TODO: This property doesn't exist on Tool class, needs to be added or removed
            // filteredTool.ParameterCount = filteredTool.Option?.Count ?? 0;
            
            return filteredTool;
        });            var toolsWithFilteredParams = (await Task.WhenAll(toolsWithFilteredParamsTasks)).ToList();

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
        var commonParamsFile = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "common-parameters.json");
        
        if (!File.Exists(commonParamsFile))
        {
            Console.WriteLine($"Warning: common-parameters.json not found at {commonParamsFile}");
            return new List<CommonParameter>();
        }

        var json = await File.ReadAllTextAsync(commonParamsFile);
        var commonParams = JsonSerializer.Deserialize<List<CommonParameterDefinition>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? new List<CommonParameterDefinition>();

        Console.WriteLine($"Loaded {commonParams.Count} common parameters from configuration file");

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

            // Generate in common-general directory
            var commonGeneralDir = Path.Combine(Path.GetDirectoryName(outputDir) ?? outputDir, "common-general");
            var outputFile = Path.Combine(commonGeneralDir, "service-start-option.md");
            await File.WriteAllTextAsync(outputFile, result);
            Console.WriteLine($"Generated service options page: common-general/service-start-option.md");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating service options page: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
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
    /// Generates individual annotation files for each tool.
    /// </summary>
    private static async Task GenerateAnnotationFilesAsync(TransformedData data, string outputDir, string templateFile, object? examplePromptGenerator = null, string? examplePromptsDir = null)
    {
        try
        {
            Console.WriteLine($"Generating annotation files for {data.Tools.Count} tools...");
            
            int examplePromptsGenerated = 0;
            int examplePromptsFailed = 0;
            
            // Log example prompts configuration
            Console.WriteLine($"DEBUG: examplePromptGenerator is {(examplePromptGenerator == null ? "NULL" : "initialized")}");
            Console.WriteLine($"DEBUG: examplePromptsDir = '{examplePromptsDir ?? "NULL"}'");
            if (examplePromptGenerator != null && !string.IsNullOrEmpty(examplePromptsDir))
            {
                Console.WriteLine($"DEBUG: Example prompts WILL be generated for each tool");
            }
            else
            {
                Console.WriteLine($"DEBUG: Example prompts WILL NOT be generated (missing generator or directory)");
            }
            
            // Track missing brand mappings/compound words
            var missingMappings = new Dictionary<string, List<string>>(); // area -> list of tool commands
            
            // Load brand mappings
            var brandMappings = await LoadBrandMappingsAsync();
            
            foreach (var tool in data.Tools)
            {
                if (string.IsNullOrEmpty(tool.Command))
                    continue;

                // Parse command to extract area (first part)
                var commandParts = tool.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (commandParts.Length == 0)
                    continue;

                var area = commandParts[0];
                
                // Load compound words for area name transformation
                var compoundWords = await LoadCompoundWordsAsync();
                
                // Get brand-based filename from mapping, or fall back to area name
                string brandFileName;
                if (brandMappings.TryGetValue(area, out var mapping) && !string.IsNullOrEmpty(mapping.FileName))
                {
                    brandFileName = mapping.FileName;
                }
                else
                {
                    // Fallback: use area name, but check compound words first
                    var areaLower = area.ToLowerInvariant();
                    if (compoundWords.TryGetValue(areaLower, out var compoundReplacement))
                    {
                        brandFileName = compoundReplacement;
                        Console.WriteLine($"Applied compound word transformation for '{area}': '{areaLower}' -> '{brandFileName}'");
                    }
                    else
                    {
                        brandFileName = areaLower;
                        Console.WriteLine($"Warning: No brand mapping or compound word found for area '{area}', using '{brandFileName}'");
                        
                        // Track missing mapping
                        if (!missingMappings.ContainsKey(area))
                        {
                            missingMappings[area] = new List<string>();
                        }
                        missingMappings[area].Add(tool.Command ?? "unknown");
                    }
                }

                // Build remaining parts of command (tool family and operation)
                var remainingParts = commandParts.Length > 1 
                    ? string.Join("-", commandParts.Skip(1)).ToLowerInvariant()
                    : "";

                // Clean the filename to remove stop words and separate smashed words
                var cleanedRemainingParts = !string.IsNullOrEmpty(remainingParts) 
                    ? await CleanFileNameAsync(remainingParts) 
                    : "";

                // Create filename: {brand-filename}-{tool-family}-{operation}-annotations.md
                // Example: azure-container-registry-registry-list-annotations.md
                var fileName = !string.IsNullOrEmpty(cleanedRemainingParts)
                    ? $"{brandFileName}-{cleanedRemainingParts}-annotations.md"
                    : $"{brandFileName}-annotations.md";
                
                var outputFile = Path.Combine(outputDir, fileName);

                // Format metadata with display names for each property
                var formattedMetadata = new Dictionary<string, object>();
                var metadata = tool.Metadata ?? new ToolMetadata();
                
                if (metadata.Destructive != null)
                {
                    formattedMetadata["destructive"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("destructive"),
                        value = metadata.Destructive.Value,
                        description = metadata.Destructive.Description
                    };
                }
                
                if (metadata.Idempotent != null)
                {
                    formattedMetadata["idempotent"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("idempotent"),
                        value = metadata.Idempotent.Value,
                        description = metadata.Idempotent.Description
                    };
                }
                
                if (metadata.OpenWorld != null)
                {
                    formattedMetadata["openWorld"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("openWorld"),
                        value = metadata.OpenWorld.Value,
                        description = metadata.OpenWorld.Description
                    };
                }
                
                if (metadata.ReadOnly != null)
                {
                    formattedMetadata["readOnly"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("readOnly"),
                        value = metadata.ReadOnly.Value,
                        description = metadata.ReadOnly.Description
                    };
                }
                
                if (metadata.Secret != null)
                {
                    formattedMetadata["secret"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("secret"),
                        value = metadata.Secret.Value,
                        description = metadata.Secret.Description
                    };
                }
                
                if (metadata.LocalRequired != null)
                {
                    formattedMetadata["localRequired"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("localRequired"),
                        value = metadata.LocalRequired.Value,
                        description = metadata.LocalRequired.Description
                    };
                }

                var annotationData = new Dictionary<string, object>
                {
                    ["tool"] = tool,
                    ["metadata"] = formattedMetadata,
                    ["command"] = tool.Command ?? "",
                    ["area"] = tool.Area ?? "",
                    ["generateAnnotation"] = true,
                    ["generatedAt"] = data.GeneratedAt,
                    ["version"] = data.Version ?? "unknown",
                    ["annotationFileName"] = fileName
                };

                var templateResult = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, annotationData);
                var frontmatter = FrontmatterUtility.GenerateAnnotationFrontmatter(
                    tool.Command ?? "unknown",
                    data.Version,
                    fileName);
                var result = frontmatter + templateResult;
                await File.WriteAllTextAsync(outputFile, result);
                tool.HasAnnotation = true;
                
                // Generate example prompts if requested
                var (successCount, failureCount) = await GenerateSingleExamplePromptAsync(tool, examplePromptGenerator, examplePromptsDir, fileName, data.Version);
                examplePromptsGenerated += successCount;
                examplePromptsFailed += failureCount;
            }
            
            Console.WriteLine($"Generated {data.Tools.Count} annotation files in {outputDir}");
            
            if (examplePromptGenerator != null)
            {
                Console.WriteLine($"\n=== Example Prompts Summary ===");
                Console.WriteLine($"  Total tools processed: {data.Tools.Count}");
                Console.WriteLine($"  Successfully generated: {examplePromptsGenerated}");
                Console.WriteLine($"  Failed: {examplePromptsFailed}");
                Console.WriteLine($"  Output directory: {examplePromptsDir}");
            }
            
            // Generate missing mappings report
            if (missingMappings.Any())
            {
                await GenerateMissingMappingsReportAsync(missingMappings, outputDir);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating annotation files: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    /// <summary>
    /// Generates a single example prompt file for a tool.
    /// DEPRECATED: Use ExamplePromptGeneratorStandalone package instead.
    /// </summary>
    /// <returns>Tuple of (successCount, failureCount) - either (1,0) or (0,1) or (0,0)</returns>
    private static Task<(int successCount, int failureCount)> GenerateSingleExamplePromptAsync(
        Tool tool, 
        object? examplePromptGenerator, 
        string? examplePromptsDir, 
        string annotationFileName, 
        string? version)
    {
        if (examplePromptGenerator == null || string.IsNullOrEmpty(examplePromptsDir))
            return Task.FromResult((0, 0));

        // DEPRECATED: Delegate to ExamplePromptGenerator, passing in the Handlebars template processor
        // Use ExamplePromptGeneratorStandalone package instead
        /*
        return await examplePromptGenerator.GenerateExamplePromptFileAsync(
            tool,
            examplePromptsDir,
            annotationFileName,
            version,
            HandlebarsTemplateEngine.ProcessTemplateAsync);
        */
        return Task.FromResult((0, 0));
    }

    /// <summary>
    /// Generates individual parameter files for each tool.
    /// </summary>
    private static async Task GenerateParameterFilesAsync(TransformedData data, string outputDir, string templateFile)
    {
        try
        {
            Console.WriteLine($"Generating parameter files for {data.Tools.Count} tools...");
            
            // Load brand mappings
            var brandMappings = await LoadBrandMappingsAsync();
            
            foreach (var tool in data.Tools)
            {
                if (string.IsNullOrEmpty(tool.Command))
                    continue;

                // Parse command to extract area (first part)
                var commandParts = tool.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (commandParts.Length == 0)
                    continue;

                var area = commandParts[0];
                
                // Load compound words for area name transformation
                var compoundWords = await LoadCompoundWordsAsync();
                
                // Get brand-based filename from mapping, or fall back to area name
                string brandFileName;
                if (brandMappings.TryGetValue(area, out var mapping) && !string.IsNullOrEmpty(mapping.FileName))
                {
                    brandFileName = mapping.FileName;
                }
                else
                {
                    // Fallback: use area name, but check compound words first
                    var areaLower = area.ToLowerInvariant();
                    if (compoundWords.TryGetValue(areaLower, out var compoundReplacement))
                    {
                        brandFileName = compoundReplacement;
                    }
                    else
                    {
                        brandFileName = areaLower;
                    }
                }

                // Build remaining parts of command (tool family and operation)
                var remainingParts = commandParts.Length > 1 
                    ? string.Join("-", commandParts.Skip(1)).ToLowerInvariant()
                    : "";

                // Clean the filename to remove stop words and separate smashed words
                var cleanedRemainingParts = !string.IsNullOrEmpty(remainingParts) 
                    ? await CleanFileNameAsync(remainingParts) 
                    : "";

                // Create filename: {brand-filename}-{tool-family}-{operation}-parameters.md
                var fileName = !string.IsNullOrEmpty(cleanedRemainingParts)
                    ? $"{brandFileName}-{cleanedRemainingParts}-parameters.md"
                    : $"{brandFileName}-parameters.md";
                
                var outputFile = Path.Combine(outputDir, fileName);

                // Transform options to include RequiredText
                var transformedOptions = tool.Option?.Select(opt => new
                {
                    name = opt.Name,
                    NL_Name = TextCleanup.NormalizeParameter(opt.Name ?? ""),
                    type = opt.Type,
                    required = opt.Required,
                    RequiredText = opt.Required == true ? "Required" : "Optional",
                    description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(opt.Description ?? ""))
                }).ToList();

                var parameterData = new Dictionary<string, object>
                {
                    ["tool"] = tool,
                    ["command"] = tool.Command ?? "",
                    ["area"] = tool.Area ?? "",
                    ["option"] = (object?)transformedOptions ?? new List<object>(),
                    ["generateParameter"] = true,
                    ["generatedAt"] = data.GeneratedAt,
                    ["version"] = data.Version ?? "unknown",
                    ["parameterFileName"] = fileName
                };

                var templateResult = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, parameterData);
                var frontmatter = FrontmatterUtility.GenerateParameterFrontmatter(
                    tool.Command ?? "unknown",
                    data.Version,
                    fileName);
                var result = frontmatter + templateResult;
                await File.WriteAllTextAsync(outputFile, result);
                tool.HasParameters = true;
            }
            
            Console.WriteLine($"Generated {data.Tools.Count} parameter files in {outputDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating parameter files: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    // DEPRECATED: Combined parameter and annotation file generation
    // Keeping method for reference but disabled.
    // Use separate annotations and parameters files instead (or complete tool files with --complete-tools flag)
    /*
    /// <summary>
    /// Generates combined parameter and annotation files for each tool.
    /// </summary>
    private static async Task GenerateParamAnnotationFilesAsync(TransformedData data, string outputDir, string templateFile)
    {
        try
        {
            Console.WriteLine($"Generating parameter and annotation files for {data.Tools.Count} tools...");
            
            // Load brand mappings
            var brandMappings = await LoadBrandMappingsAsync();
            
            foreach (var tool in data.Tools)
            {
                if (string.IsNullOrEmpty(tool.Command))
                    continue;

                // Parse command to extract area (first part)
                var commandParts = tool.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (commandParts.Length == 0)
                    continue;

                var area = commandParts[0];
                
                // Load compound words for area name transformation
                var compoundWords = await LoadCompoundWordsAsync();
                
                // Get brand-based filename from mapping, or fall back to area name
                string brandFileName;
                if (brandMappings.TryGetValue(area, out var mapping) && !string.IsNullOrEmpty(mapping.FileName))
                {
                    brandFileName = mapping.FileName;
                }
                else
                {
                    // Fallback: use area name, but check compound words first
                    var areaLower = area.ToLowerInvariant();
                    if (compoundWords.TryGetValue(areaLower, out var compoundReplacement))
                    {
                        brandFileName = compoundReplacement;
                    }
                    else
                    {
                        brandFileName = areaLower;
                    }
                }

                // Build remaining parts of command (tool family and operation)
                var remainingParts = commandParts.Length > 1 
                    ? string.Join("-", commandParts.Skip(1)).ToLowerInvariant()
                    : "";

                // Clean the filename to remove stop words and separate smashed words
                var cleanedRemainingParts = !string.IsNullOrEmpty(remainingParts) 
                    ? await CleanFileNameAsync(remainingParts) 
                    : "";

                // Create filename: {brand-filename}-{tool-family}-{operation}-param-annotation.md
                var fileName = !string.IsNullOrEmpty(cleanedRemainingParts)
                    ? $"{brandFileName}-{cleanedRemainingParts}-param-annotation.md"
                    : $"{brandFileName}-param-annotation.md";
                
                var outputFile = Path.Combine(outputDir, fileName);

                // Format metadata with display names for each property
                var formattedMetadata = new Dictionary<string, object>();
                var metadata = tool.Metadata ?? new ToolMetadata();
                
                if (metadata.Destructive != null)
                {
                    formattedMetadata["destructive"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("destructive"),
                        value = metadata.Destructive.Value,
                        description = metadata.Destructive.Description
                    };
                }
                
                if (metadata.Idempotent != null)
                {
                    formattedMetadata["idempotent"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("idempotent"),
                        value = metadata.Idempotent.Value,
                        description = metadata.Idempotent.Description
                    };
                }
                
                if (metadata.OpenWorld != null)
                {
                    formattedMetadata["openWorld"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("openWorld"),
                        value = metadata.OpenWorld.Value,
                        description = metadata.OpenWorld.Description
                    };
                }
                
                if (metadata.ReadOnly != null)
                {
                    formattedMetadata["readOnly"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("readOnly"),
                        value = metadata.ReadOnly.Value,
                        description = metadata.ReadOnly.Description
                    };
                }
                
                if (metadata.Secret != null)
                {
                    formattedMetadata["secret"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("secret"),
                        value = metadata.Secret.Value,
                        description = metadata.Secret.Description
                    };
                }
                
                if (metadata.LocalRequired != null)
                {
                    formattedMetadata["localRequired"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("localRequired"),
                        value = metadata.LocalRequired.Value,
                        description = metadata.LocalRequired.Description
                    };
                }

                // Transform options to include RequiredText
                var transformedOptions = tool.Option?.Select(opt => new
                {
                    name = opt.Name,
                    NL_Name = TextCleanup.NormalizeParameter(opt.Name ?? ""),
                    type = opt.Type,
                    required = opt.Required,
                    RequiredText = opt.Required == true ? "Required" : "Optional",
                    description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(opt.Description ?? ""))
                }).ToList();

                var paramAnnotationData = new Dictionary<string, object>
                {
                    ["tool"] = tool,
                    ["metadata"] = formattedMetadata,
                    ["command"] = tool.Command ?? "",
                    ["area"] = tool.Area ?? "",
                    ["option"] = (object?)transformedOptions ?? new List<object>(),
                    ["generateParameterAndAnnotation"] = true,
                    ["generatedAt"] = data.GeneratedAt,
                    ["version"] = data.Version ?? "unknown",
                    ["paramAnnotationFileName"] = fileName
                };

                var templateResult = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, paramAnnotationData);
                var frontmatter = FrontmatterUtility.GenerateGenericFrontmatter(
                    "include",
                    data.Version,
                    new Dictionary<string, string>
                    {
                        ["comment"] = $"[!INCLUDE [{tool.Command ?? "unknown"}](../includes/tools/param-and-annotation/{fileName})]",
                        ["azmcp"] = $"# azmcp {tool.Command ?? "unknown"}"
                    });
                var result = frontmatter + templateResult;
                await File.WriteAllTextAsync(outputFile, result);
            }
            
            Console.WriteLine($"Generated {data.Tools.Count} parameter and annotation files in {outputDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating parameter and annotation files: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
    */

    /// <summary>
    /// Generates a comprehensive metadata report covering all tool characteristics.
    /// </summary>
    private static async Task GenerateSecurityReportsAsync(TransformedData data, string outputDir)
    {
        try
        {
            // Find tools with various metadata characteristics
            var secretsRequiredTools = data.Tools
                .Where(t => t.Metadata?.Secret?.Value == true)
                .OrderBy(t => t.Command)
                .ToList();

            var localConsentTools = data.Tools
                .Where(t => t.Metadata?.LocalRequired?.Value == true)
                .OrderBy(t => t.Command)
                .ToList();

            var destructiveTools = data.Tools
                .Where(t => t.Metadata?.Destructive?.Value == true)
                .OrderBy(t => t.Command)
                .ToList();

            var readOnlyTools = data.Tools
                .Where(t => t.Metadata?.ReadOnly?.Value == true)
                .OrderBy(t => t.Command)
                .ToList();

            var nonIdempotentTools = data.Tools
                .Where(t => t.Metadata?.Idempotent?.Value == false)
                .OrderBy(t => t.Command)
                .ToList();

            // Find tools that have both secrets and local consent requirements
            var bothRequirementsTools = data.Tools
                .Where(t => t.Metadata?.Secret?.Value == true && t.Metadata?.LocalRequired?.Value == true)
                .OrderBy(t => t.Command)
                .ToList();

            // Generate comprehensive metadata report
            var reportLines = new List<string>
            {
                "# Azure MCP Tools Metadata Report",
                "",
                $"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                $"**Total Tools:** {data.Tools.Count}",
                "",
                "This comprehensive report provides detailed metadata analysis for all Azure MCP tools, including security requirements, operational characteristics, and safety considerations.",
                "",
                "## Executive Summary",
                "",
                "| Characteristic | Count | Percentage | Description |",
                "|----------------|-------|------------|-------------|",
                $"| Secrets Required | {secretsRequiredTools.Count} | {(secretsRequiredTools.Count * 100.0 / data.Tools.Count):F1}% | Tools that handle sensitive information |",
                $"| Local Consent Required | {localConsentTools.Count} | {(localConsentTools.Count * 100.0 / data.Tools.Count):F1}% | Tools requiring explicit user consent |",
                $"| Destructive Operations | {destructiveTools.Count} | {(destructiveTools.Count * 100.0 / data.Tools.Count):F1}% | Tools that can delete or modify resources |",
                $"| Read-Only Operations | {readOnlyTools.Count} | {(readOnlyTools.Count * 100.0 / data.Tools.Count):F1}% | Tools that only read data without modifications |",
                $"| Non-Idempotent | {nonIdempotentTools.Count} | {(nonIdempotentTools.Count * 100.0 / data.Tools.Count):F1}% | Tools where repeated calls may have different effects |",
                $"| High-Risk (Secrets + Consent) | {bothRequirementsTools.Count} | {(bothRequirementsTools.Count * 100.0 / data.Tools.Count):F1}% | Tools requiring both secrets and user consent |",
                ""
            };

            // Security Requirements Section
            reportLines.AddRange(new[]
            {
                "## Security Requirements",
                "",
                "### Tools Requiring Secrets",
                "",
                $"**Count:** {secretsRequiredTools.Count} tools",
                "",
                "These tools handle sensitive information like passwords, keys, or tokens and require secure handling.",
                ""
            });

            if (secretsRequiredTools.Any())
            {
                // Group by area for secrets
                var secretsByArea = secretsRequiredTools
                    .GroupBy(t => t.Command?.Split(' ')[0] ?? "unknown")
                    .OrderBy(g => g.Key)
                    .ToList();

                reportLines.Add("**Summary by Service Area:**");
                reportLines.Add("");
                foreach (var areaGroup in secretsByArea)
                {
                    reportLines.Add($"- **{areaGroup.Key}:** {areaGroup.Count()} tools");
                }

                reportLines.AddRange(new[]
                {
                    "",
                    "**Detailed List:**",
                    "",
                    "| Command | Area | Description |",
                    "|---------|------|-------------|"
                });

                foreach (var tool in secretsRequiredTools)
                {
                    var area = tool.Command?.Split(' ')[0] ?? "unknown";
                    var description = tool.Description?.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "") ?? "";
                    if (description.Length > 100)
                    {
                        description = description.Substring(0, 97) + "...";
                    }
                    reportLines.Add($"| `{tool.Command}` | {area} | {description} |");
                }
                reportLines.Add("");
            }
            else
            {
                reportLines.Add("*No tools require secrets handling.*");
                reportLines.Add("");
            }

            // Local Consent Requirements Section
            reportLines.AddRange(new[]
            {
                "### Tools Requiring Local User Consent",
                "",
                $"**Count:** {localConsentTools.Count} tools",
                "",
                "These tools require explicit local user consent or authentication before execution.",
                ""
            });

            if (localConsentTools.Any())
            {
                // Group by area for local consent
                var localConsentByArea = localConsentTools
                    .GroupBy(t => t.Command?.Split(' ')[0] ?? "unknown")
                    .OrderBy(g => g.Key)
                    .ToList();

                reportLines.Add("**Summary by Service Area:**");
                reportLines.Add("");
                foreach (var areaGroup in localConsentByArea)
                {
                    reportLines.Add($"- **{areaGroup.Key}:** {areaGroup.Count()} tools");
                }

                reportLines.AddRange(new[]
                {
                    "",
                    "**Detailed List:**",
                    "",
                    "| Command | Area | Description |",
                    "|---------|------|-------------|"
                });

                foreach (var tool in localConsentTools)
                {
                    var area = tool.Command?.Split(' ')[0] ?? "unknown";
                    var description = tool.Description?.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "") ?? "";
                    if (description.Length > 100)
                    {
                        description = description.Substring(0, 97) + "...";
                    }
                    reportLines.Add($"| `{tool.Command}` | {area} | {description} |");
                }
                reportLines.Add("");
            }
            else
            {
                reportLines.Add("*No tools require local user consent.*");
                reportLines.Add("");
            }

            // High-Risk Tools (Both Requirements)
            if (bothRequirementsTools.Any())
            {
                reportLines.AddRange(new[]
                {
                    "### High-Risk Tools (Secrets + Consent Required)",
                    "",
                    $"**Count:** {bothRequirementsTools.Count} tools",
                    "",
                    "These tools require both secrets handling and local user consent, representing the highest security risk category.",
                    "",
                    "| Command | Area | Description |",
                    "|---------|------|-------------|"
                });

                foreach (var tool in bothRequirementsTools)
                {
                    var area = tool.Command?.Split(' ')[0] ?? "unknown";
                    var description = tool.Description?.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "") ?? "";
                    if (description.Length > 100)
                    {
                        description = description.Substring(0, 97) + "...";
                    }
                    reportLines.Add($"| `{tool.Command}` | {area} | {description} |");
                }
                reportLines.Add("");
            }

            // Operational Characteristics Section
            reportLines.AddRange(new[]
            {
                "## Operational Characteristics",
                "",
                "### Destructive Operations",
                "",
                $"**Count:** {destructiveTools.Count} tools",
                "",
                "These tools can delete, modify, or irreversibly change Azure resources."
            });

            if (destructiveTools.Any())
            {
                reportLines.AddRange(new[]
                {
                    "",
                    "| Command | Area | Description |",
                    "|---------|------|-------------|"
                });

                foreach (var tool in destructiveTools.Take(10)) // Limit to first 10 to keep report manageable
                {
                    var area = tool.Command?.Split(' ')[0] ?? "unknown";
                    var description = tool.Description?.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "") ?? "";
                    if (description.Length > 100)
                    {
                        description = description.Substring(0, 97) + "...";
                    }
                    reportLines.Add($"| `{tool.Command}` | {area} | {description} |");
                }

                if (destructiveTools.Count > 10)
                {
                    reportLines.Add($"| ... | ... | *{destructiveTools.Count - 10} additional destructive tools* |");
                }
            }

            reportLines.AddRange(new[]
            {
                "",
                "### Read-Only Operations",
                "",
                $"**Count:** {readOnlyTools.Count} tools",
                "",
                "These tools only read data and do not modify any Azure resources."
            });

            if (readOnlyTools.Any())
            {
                reportLines.AddRange(new[]
                {
                    "",
                    "| Command | Area | Description |",
                    "|---------|------|-------------|"
                });

                foreach (var tool in readOnlyTools.Take(15)) // Show first 15 read-only tools
                {
                    var area = tool.Command?.Split(' ')[0] ?? "unknown";
                    var description = tool.Description?.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "") ?? "";
                    if (description.Length > 100)
                    {
                        description = description.Substring(0, 97) + "...";
                    }
                    reportLines.Add($"| `{tool.Command}` | {area} | {description} |");
                }

                if (readOnlyTools.Count > 15)
                {
                    reportLines.Add($"| ... | ... | *{readOnlyTools.Count - 15} additional read-only tools* |");
                }
            }

            reportLines.AddRange(new[]
            {
                "",
                "### Non-Idempotent Operations",
                "",
                $"**Count:** {nonIdempotentTools.Count} tools",
                "",
                "These tools may produce different results when called multiple times with the same parameters."
            });

            if (nonIdempotentTools.Any())
            {
                reportLines.AddRange(new[]
                {
                    "",
                    "| Command | Area | Description |",
                    "|---------|------|-------------|"
                });

                foreach (var tool in nonIdempotentTools.Take(15)) // Show first 15 non-idempotent tools
                {
                    var area = tool.Command?.Split(' ')[0] ?? "unknown";
                    var description = tool.Description?.Replace("|", "\\|").Replace("\n", " ").Replace("\r", "") ?? "";
                    if (description.Length > 100)
                    {
                        description = description.Substring(0, 97) + "...";
                    }
                    reportLines.Add($"| `{tool.Command}` | {area} | {description} |");
                }

                if (nonIdempotentTools.Count > 15)
                {
                    reportLines.Add($"| ... | ... | *{nonIdempotentTools.Count - 15} additional non-idempotent tools* |");
                }
            }

            reportLines.AddRange(new[]
            {
                "",
                "---",
                "",
                "*This report is automatically generated from Azure MCP Server source code metadata.*"
            });

            // Save the comprehensive report
            var reportDir = Path.Combine(outputDir, "reports");
            Directory.CreateDirectory(reportDir);
            var reportPath = Path.Combine(reportDir, "tools-metadata-report.md");
            await File.WriteAllTextAsync(reportPath, string.Join("\n", reportLines));

            Console.WriteLine($"Generated comprehensive metadata report:");
            Console.WriteLine($"  - {Path.GetFileName(reportPath)} (covering {data.Tools.Count} tools)");
            Console.WriteLine($"    • {secretsRequiredTools.Count} tools requiring secrets");
            Console.WriteLine($"    • {localConsentTools.Count} tools requiring local consent");
            Console.WriteLine($"    • {destructiveTools.Count} destructive operations");
            Console.WriteLine($"    • {readOnlyTools.Count} read-only operations");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating metadata report: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    /// <summary>
    /// Cleans a filename by removing stop words and separating smashed words.
    /// </summary>
    private static async Task<string> CleanFileNameAsync(string fileName)
    {
        // Load stop words and compound words from JSON files
        var stopWords = await LoadStopWordsAsync();
        var compoundWords = await LoadCompoundWordsAsync();
        
        // Split by hyphens
        var parts = fileName.Split('-');
        var cleanedParts = new List<string>();
        
        foreach (var part in parts)
        {
            // Skip empty parts
            if (string.IsNullOrWhiteSpace(part))
                continue;
                
            // Check if this is a compound word that needs separation
            var lowerPart = part.ToLowerInvariant();
            if (compoundWords.ContainsKey(lowerPart))
            {
                // Split the compound word and add each piece separately
                var separated = compoundWords[lowerPart].Split('-');
                foreach (var subPart in separated)
                {
                    if (!stopWords.Contains(subPart.ToLowerInvariant()))
                    {
                        cleanedParts.Add(subPart);
                    }
                }
            }
            else
            {
                // Remove stop words
                if (!stopWords.Contains(lowerPart))
                {
                    cleanedParts.Add(part);
                }
            }
        }
        
        return string.Join("-", cleanedParts);
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
