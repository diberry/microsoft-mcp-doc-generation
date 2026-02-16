// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using GenerativeAI;
using CSharpGenerator;
using CSharpGenerator.Models;
using HorizontalArticleGenerator.Models;
using Shared;
using Azure.Mcp.TextTransformation.Models;
using Azure.Mcp.TextTransformation.Services;

namespace HorizontalArticleGenerator.Generators;

/// <summary>
/// Generates horizontal how-to articles for Azure services using AI content generation.
/// Extracts static data from CLI output, generates AI content, merges, and renders templates.
/// </summary>
public class HorizontalArticleGenerator
{
    // Extracted method for generating a single article
    private async Task<bool> GenerateSingleArticleAsync(StaticArticleData staticData, string outputDir, string progress)
    {
        try
        {
            Console.WriteLine($"{progress} Processing {staticData.ServiceBrandName}...");
            // Generate AI content
            var aiResponse = await GenerateAIContent(staticData);
            AIGeneratedArticleData? aiData = null;
            bool parseFailed = false;
            try
            {
                aiData = ParseAIResponse(aiResponse);
            }
            catch (Exception jsonEx)
            {
                Console.WriteLine($"{progress} ✗ Failed to parse AI response for {staticData.ServiceBrandName}: {jsonEx.Message}");
                var errorLog = Path.Combine(outputDir, $"error-{staticData.ServiceIdentifier}-airesponse.txt");
                await File.WriteAllTextAsync(errorLog, $"Raw AI response:\n{aiResponse}\n\nError: {jsonEx.Message}\n{jsonEx.StackTrace}");
                Console.WriteLine($"Raw AI response logged to: {errorLog}");
                Console.WriteLine();
                parseFailed = true;
            }
            if (parseFailed || aiData == null) return false; // Skip this article
            
            // Validate AI-generated content for common issues (blocks on critical errors)
            try
            {
                ValidateAIContent(aiData, staticData.ServiceBrandName, progress);
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine($"{progress} ✗ Validation failed: {ex.Message}");
                Console.WriteLine();
                return false; // Skip this article due to critical errors
            }
            
            // Apply text transformations if engine is available
            ApplyTextTransformations(aiData);
            
            // Merge static + AI data
            var templateData = MergeData(staticData, aiData);
            // Render and save
            await RenderAndSaveArticle(templateData);
            Console.WriteLine($"{progress} ✓ Generated: horizontal-article-{staticData.ServiceIdentifier}.md");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{progress} ✗ Failed for {staticData.ServiceBrandName}: {ex.Message}");
            // Log detailed error
            var errorLog = Path.Combine(outputDir, $"error-{staticData.ServiceIdentifier}.txt");
            await File.WriteAllTextAsync(errorLog, $"{ex.Message}\n\n{ex.StackTrace}");
            Console.WriteLine();
            return false;
        }
    }
    private const string CLI_OUTPUT_PATH = "../generated/cli/cli-output.json";
    private const string CLI_VERSION_PATH = "../generated/cli/cli-version.json";
    private const string OUTPUT_DIR = "../generated/horizontal-articles";
    private const string PROMPT_OUTPUT_DIR = "../generated/horizontal-article-prompts";
    private const string SYSTEM_PROMPT_PATH = "./HorizontalArticleGenerator/prompts/horizontal-article-system-prompt.txt";
    private const string USER_PROMPT_PATH = "./HorizontalArticleGenerator/prompts/horizontal-article-user-prompt.txt";
    private const string TEMPLATE_PATH = "./HorizontalArticleGenerator/templates/horizontal-article-template.hbs";
    
    private readonly GenerativeAIClient _aiClient;
    private readonly bool _useTextTransformation;
    private readonly bool _generateAllArticles;
    private readonly TransformationEngine? _transformationEngine;

    public HorizontalArticleGenerator(GenerativeAIOptions options, bool useTextTransformation = false, bool generateAllArticles = false, TransformationEngine? transformationEngine = null)
    {
        if (string.IsNullOrEmpty(options.ApiKey)) throw new InvalidOperationException("FOUNDRY_API_KEY not set");
        if (string.IsNullOrEmpty(options.Endpoint)) throw new InvalidOperationException("FOUNDRY_ENDPOINT not set");
        if (string.IsNullOrEmpty(options.Deployment)) throw new InvalidOperationException("FOUNDRY_MODEL_NAME not set");
        if (string.IsNullOrEmpty(options.ApiVersion)) throw new InvalidOperationException("FOUNDRY_MODEL_API_VERSION not set");
        _aiClient = new GenerativeAIClient(options);
        _useTextTransformation = useTextTransformation;
        _generateAllArticles = generateAllArticles;
        _transformationEngine = transformationEngine;
    }
    
    /// <summary>
    /// Main entry point: generates all horizontal articles for all services
    /// </summary>
    public async Task GenerateAllArticles()
    {
        Console.WriteLine("=== Horizontal Article Generation ===");
        Console.WriteLine();
        
        // Phase 1: Extract static data
        Console.WriteLine("Phase 1: Extracting static data from CLI output...");
        var staticDataList = await ExtractStaticData();
        Console.WriteLine($"✓ Found {staticDataList.Count} services");
        Console.WriteLine();
        // Create output directory
        var outputDir = Path.GetFullPath(OUTPUT_DIR);
        Directory.CreateDirectory(outputDir);
        Console.WriteLine($"Output directory: {outputDir}");
        Console.WriteLine();
        // Phase 2 & 3: Generate AI content and render for each service
        Console.WriteLine("Phase 2-3: Generating AI content and rendering articles...");
        Console.WriteLine();
        var successCount = 0;
        var failureCount = 0;

        if (_generateAllArticles)
        {
            for (int i = 0; i < staticDataList.Count; i++)
            {
                var staticData = staticDataList[i];
                var progress = $"[{i + 1}/{staticDataList.Count}]";
                bool result = await GenerateSingleArticleAsync(staticData, outputDir, progress);
                if (result) successCount++;
                else failureCount++;
            }
        }
        else
        {
            // Only generate the first article
            if (staticDataList.Count > 0)
            {
                var staticData = staticDataList[0];
                var progress = "[1/1]";
                bool result = await GenerateSingleArticleAsync(staticData, outputDir, progress);
                if (result) successCount++;
                else failureCount++;
            }
        }
    }

    /// <summary>
    /// Generate a single service's horizontal article
    /// </summary>
    public async Task GenerateSingleServiceArticle(string serviceArea)
    {
        // Phase 1: Extract static data
        var staticDataList = await ExtractStaticData();
        
        // Find the requested service
        var targetService = staticDataList.FirstOrDefault(s => 
            s.ServiceIdentifier.Equals(serviceArea, StringComparison.OrdinalIgnoreCase));
        
        if (targetService == null)
        {
            Console.Error.WriteLine($"✗ Service not found: {serviceArea}");
            Console.Error.WriteLine($"Available services: {string.Join(", ", staticDataList.Select(s => s.ServiceIdentifier))}");
            return;
        }
        
        // Create output directory
        var outputDir = Path.GetFullPath(OUTPUT_DIR);
        Directory.CreateDirectory(outputDir);
        
        // Generate the article
        bool result = await GenerateSingleArticleAsync(targetService, outputDir, "[1/1]");
        
        if (!result)
        {
            Console.Error.WriteLine($"✗ Single service article generation failed for {serviceArea}");
        }
    }

    /// <summary>
    /// Extracts static data for all services from CLI output and transformation config
    /// </summary>
    private async Task<List<StaticArticleData>> ExtractStaticData()
    {
        var serviceDataList = new List<StaticArticleData>();

        // Load CLI output
        var cliOutputPath = Path.GetFullPath(CLI_OUTPUT_PATH);
        if (!File.Exists(cliOutputPath))
        {
            throw new FileNotFoundException($"CLI output not found: {cliOutputPath}");
        }

        var jsonContent = await File.ReadAllTextAsync(cliOutputPath);
        var cliData = JsonSerializer.Deserialize<CliOutput>(jsonContent, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (cliData?.Results == null)
        {
            throw new InvalidOperationException("Invalid CLI output format");
        }

        // Load version info using utility
        var baseOutputDir = Path.GetFullPath("../generated");
        var cliVersion = await CliVersionReader.ReadCliVersionAsync(baseOutputDir);

        // Group tools by service area (first word of command or name)
        var toolsByService = cliData.Results
            .Where(tool => !string.IsNullOrEmpty(tool.Command ?? tool.Name))
            .GroupBy(tool => (tool.Command ?? tool.Name)!.Split(' ')[0])
            .ToDictionary(g => g.Key, g => g.ToList());

        // Load brand mappings from brand-to-server-mapping.json (comprehensive, all services)
        var sharedBrandMappings = await DataFileLoader.LoadBrandMappingsAsync();

        if (_useTextTransformation)
        {
            // Load transformation config for additional mappings
            var configPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "transformation-config.json");
            var loader = new ConfigLoader(configPath);
            var config = await loader.LoadAsync();
            var engine = new TransformationEngine(config);

            foreach (var (serviceArea, tools) in toolsByService)
            {
                // Get brand name: transformation config > brand-to-server-mapping > fallback
                var transformMapping = engine.Config.Services.Mappings
                    .FirstOrDefault(m => m.McpName == serviceArea);

                string serviceBrandName;
                string toolsRefFile;
                if (transformMapping?.BrandName != null)
                {
                    serviceBrandName = transformMapping.BrandName;
                    toolsRefFile = transformMapping.Filename ?? serviceArea;
                }
                else if (sharedBrandMappings.TryGetValue(serviceArea, out var brandMap))
                {
                    serviceBrandName = brandMap.BrandName ?? FormatServiceName(serviceArea);
                    toolsRefFile = serviceArea;
                }
                else
                {
                    serviceBrandName = FormatServiceName(serviceArea);
                    toolsRefFile = serviceArea;
                }

                var staticData = new StaticArticleData
                {
                    ServiceBrandName = serviceBrandName,
                    ServiceIdentifier = serviceArea,
                    GeneratedAt = DateTime.UtcNow.ToString("o"),
                    Version = cliVersion,
                    ToolsReferenceLink = $"../tools/{toolsRefFile}.md",
                    Tools = tools.Select(tool => new HorizontalToolSummary
                    {
                        Command = tool.Command ?? tool.Name ?? "",
                        Description = tool.Description ?? "",
                        ParameterCount = CountNonCommonParameters(tool),
                        Metadata = ExtractMetadata(tool),
                        MoreInfoLink = $"../parameters/{(tool.Command ?? tool.Name ?? "").Replace(' ', '-')}-parameters.md"
                    }).ToList()
                };

                serviceDataList.Add(staticData);
            }
        }
        else
        {
            // Use brand-to-server-mapping.json for brand names, fallback to formatting
            foreach (var (serviceArea, tools) in toolsByService)
            {
                string serviceBrandName;
                if (sharedBrandMappings.TryGetValue(serviceArea, out var brandMap))
                {
                    serviceBrandName = brandMap.BrandName ?? FormatServiceName(serviceArea);
                }
                else
                {
                    serviceBrandName = FormatServiceName(serviceArea);
                }

                var staticData = new StaticArticleData
                {
                    ServiceBrandName = serviceBrandName,
                    ServiceIdentifier = serviceArea,
                    GeneratedAt = DateTime.UtcNow.ToString("o"),
                    Version = cliVersion,
                    ToolsReferenceLink = $"../tools/{serviceArea}.md",
                    Tools = tools.Select(tool => new HorizontalToolSummary
                    {
                        Command = tool.Command ?? tool.Name ?? "",
                        Description = tool.Description ?? "",
                        ParameterCount = CountNonCommonParameters(tool),
                        Metadata = ExtractMetadata(tool),
                        MoreInfoLink = $"../parameters/{(tool.Command ?? tool.Name ?? "").Replace(' ', '-')}-parameters.md"
                    }).ToList()
                };

                serviceDataList.Add(staticData);
            }
        }

        return serviceDataList.OrderBy(s => s.ServiceBrandName).ToList();
    }
    
    /// <summary>
    /// Count non-common parameters in a tool
    /// </summary>
    private static int CountNonCommonParameters(Tool tool)
    {
        var commonParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "subscription-id", "resource-group", "output", "verbose", 
            "help", "debug", "only-show-errors"
        };
        
        return tool.Option?.Count(o => 
            !commonParams.Contains(o.Name ?? "")) ?? 0;
    }
    
    /// <summary>
    /// Extract metadata from tool
    /// </summary>
    private static Dictionary<string, MetadataValue> ExtractMetadata(Tool tool)
    {
        // Use existing metadata if available, otherwise defaults
        var metadata = tool.Metadata ?? new ToolMetadata();
        
        return new Dictionary<string, MetadataValue>
        {
            ["destructive"] = metadata.Destructive ?? new MetadataValue { Value = false, Description = "default" },
            ["readOnly"] = metadata.ReadOnly ?? new MetadataValue { Value = false, Description = "default" },
            ["secret"] = metadata.Secret ?? new MetadataValue { Value = false, Description = "default" }
        };
    }
    
    /// <summary>
    /// Simple service name formatting
    /// </summary>
    private static string FormatServiceName(string serviceArea)
    {
        return char.ToUpper(serviceArea[0]) + serviceArea.Substring(1);
    }
    
    /// <summary>
    /// Phase 2: Generate AI content using prompts
    /// </summary>
    private async Task<string> GenerateAIContent(StaticArticleData staticData)
    {
        // Load prompts
        var systemPromptPath = Path.GetFullPath(SYSTEM_PROMPT_PATH);
        var userPromptPath = Path.GetFullPath(USER_PROMPT_PATH);
        
        var systemPrompt = await File.ReadAllTextAsync(systemPromptPath);
        var userPromptTemplate = await File.ReadAllTextAsync(userPromptPath);

        // Process user prompt with Handlebars to inject static data
        var handlebars = HandlebarsDotNet.Handlebars.Create();
        var userPromptCompiled = handlebars.Compile(userPromptTemplate);

        var promptContext = new
        {
            serviceBrandName = staticData.ServiceBrandName,
            serviceIdentifier = staticData.ServiceIdentifier,
            toolsReferenceLink = staticData.ToolsReferenceLink,
            tools = staticData.Tools.Select(t => new
            {
                command = t.Command,
                description = t.Description,
                parameterCount = t.ParameterCount,
                metadata = new
                {
                    destructive = new { value = t.Metadata.GetValueOrDefault("destructive", new MetadataValue()).Value },
                    readOnly = new { value = t.Metadata.GetValueOrDefault("readOnly", new MetadataValue()).Value },
                    secret = new { value = t.Metadata.GetValueOrDefault("secret", new MetadataValue()).Value }
                }
            })
        };

        var userPrompt = userPromptCompiled(promptContext);

        // Save prompts to output directory
        var promptDir = Path.GetFullPath(PROMPT_OUTPUT_DIR);
        Directory.CreateDirectory(promptDir);
        
        var promptFileName = $"horizontal-article-{staticData.ServiceIdentifier}-prompt.md";
        var promptFilePath = Path.Combine(promptDir, promptFileName);
        var promptContent = $"""
# Horizontal Article Prompt: {staticData.ServiceBrandName}

Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

## System Prompt

{systemPrompt}

## User Prompt

{userPrompt}
""";
        await File.WriteAllTextAsync(promptFilePath, promptContent);

        // Calculate token limit based on tool count
        // Base: 2000 tokens + 400 tokens per tool (for tool descriptions, scenarios, etc.)
        // Min: 2500, Max: 12000
        var toolCount = staticData.Tools.Count;
        var calculatedTokens = 2000 + (toolCount * 400);
        var maxTokens = Math.Clamp(calculatedTokens, 2500, 12000);

        // Call AI client
        var response = await _aiClient.GetChatCompletionAsync(
            systemPrompt,
            userPrompt,
            maxTokens
        );

        // Append AI response to prompt file
        var responseContent = $"""


## AI Response

```json
{response}
```
""";
        await File.AppendAllTextAsync(promptFilePath, responseContent);
        return response;
    }
    
    /// <summary>
    /// Parse AI response JSON
    /// </summary>
    private AIGeneratedArticleData ParseAIResponse(string aiResponse)
    {
        var jsonContent = ExtractJsonFromResponse(aiResponse);
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };
        
        return JsonSerializer.Deserialize<AIGeneratedArticleData>(jsonContent, options)
            ?? throw new InvalidOperationException("Failed to parse AI response");
    }
    
    /// <summary>
    /// Extract JSON from AI response (may be wrapped in markdown code blocks)
    /// </summary>
    private string ExtractJsonFromResponse(string response)
    {
        var trimmed = response.Trim();
        
        if (trimmed.StartsWith("```json"))
        {
            trimmed = trimmed.Substring(7);
            var endIndex = trimmed.LastIndexOf("```");
            if (endIndex > 0)
                trimmed = trimmed.Substring(0, endIndex);
        }
        else if (trimmed.StartsWith("```"))
        {
            trimmed = trimmed.Substring(3);
            var endIndex = trimmed.LastIndexOf("```");
            if (endIndex > 0)
                trimmed = trimmed.Substring(0, endIndex);
        }
        
        return trimmed.Trim();
    }
    
    /// <summary>
    /// Phase 3: Merge static and AI data
    /// </summary>
    private HorizontalArticleTemplateData MergeData(
        StaticArticleData staticData, 
        AIGeneratedArticleData aiData)
    {
        // Merge tools: match by command name
        var mergedTools = staticData.Tools.Select(staticTool =>
        {
            var aiTool = aiData.Tools?.FirstOrDefault(
                t => t.Command == staticTool.Command);
            
            return new MergedTool
            {
                Command = staticTool.Command,
                MoreInfoLink = staticTool.MoreInfoLink,
                ShortDescription = aiTool?.ShortDescription ?? staticTool.Description
            };
        }).ToList();
        
        return new HorizontalArticleTemplateData
        {
            // Static fields
            ServiceBrandName = staticData.ServiceBrandName,
            ServiceIdentifier = staticData.ServiceIdentifier,
            GeneratedAt = staticData.GeneratedAt,
            Version = staticData.Version,
            ToolsReferenceLink = staticData.ToolsReferenceLink,
            Tools = mergedTools,
            
            // AI-generated fields
            ServiceShortDescription = aiData.ServiceShortDescription,
            ServiceOverview = aiData.ServiceOverview,
            Capabilities = aiData.Capabilities,
            ServiceSpecificPrerequisites = aiData.ServiceSpecificPrerequisites,
            Scenarios = aiData.Scenarios,
            AISpecificScenarios = aiData.AISpecificScenarios,
            RequiredRoles = aiData.RequiredRoles,
            AuthenticationNotes = aiData.AuthenticationNotes,
            CommonIssues = aiData.CommonIssues,
            BestPractices = aiData.BestPractices,
            ServiceDocLink = aiData.ServiceDocLink,
            AdditionalLinks = aiData.AdditionalLinks
        };
    }
    
    /// <summary>
    /// Render template and save to file
    /// </summary>
    private async Task RenderAndSaveArticle(HorizontalArticleTemplateData templateData)
    {
        var templatePath = Path.GetFullPath(TEMPLATE_PATH);
        
        // Manually build dictionary with correct field names (including genai- prefix)
        var data = new Dictionary<string, object>
        {
            // Static fields (no prefix)
            ["serviceBrandName"] = templateData.ServiceBrandName,
            ["serviceIdentifier"] = templateData.ServiceIdentifier,
            ["generatedAt"] = templateData.GeneratedAt,
            ["version"] = templateData.Version,
            ["toolsReferenceLink"] = templateData.ToolsReferenceLink,
            
            // AI-generated fields (genai- prefix)
            ["genai-serviceShortDescription"] = templateData.ServiceShortDescription,
            ["genai-serviceOverview"] = templateData.ServiceOverview,
            ["genai-capabilities"] = templateData.Capabilities,
            ["genai-serviceSpecificPrerequisites"] = templateData.ServiceSpecificPrerequisites,
            ["genai-scenarios"] = templateData.Scenarios,
            ["genai-aiSpecificScenarios"] = templateData.AISpecificScenarios,
            ["genai-requiredRoles"] = templateData.RequiredRoles,
            ["genai-authenticationNotes"] = templateData.AuthenticationNotes,
            ["genai-commonIssues"] = templateData.CommonIssues,
            ["genai-bestPractices"] = templateData.BestPractices,
            ["genai-serviceDocLink"] = templateData.ServiceDocLink,
            ["genai-additionalLinks"] = templateData.AdditionalLinks,
            
            // Merged tools - convert to dictionaries for Handlebars
            ["tools"] = templateData.Tools.Select(t => new Dictionary<string, object>
            {
                ["command"] = t.Command,
                ["moreInfoLink"] = t.MoreInfoLink,
                ["genai-shortDescription"] = t.ShortDescription
            }).ToList()
        };
        
        var renderedContent = await HandlebarsTemplateEngine.ProcessTemplateAsync(templatePath, data);
        
        // Save to file
        var outputDir = Path.GetFullPath(OUTPUT_DIR);
        var filename = $"horizontal-article-{templateData.ServiceIdentifier}.md";
        var outputPath = Path.Combine(outputDir, filename);
        
        await File.WriteAllTextAsync(outputPath, renderedContent);
    }

    /// <summary>
    /// Apply text transformations to AI-generated content
    /// </summary>
    private void ApplyTextTransformations(AIGeneratedArticleData aiData)
    {
        if (_transformationEngine == null)
            return;

        try
        {
            // Transform main descriptions
            aiData.ServiceShortDescription = _transformationEngine.TransformDescription(aiData.ServiceShortDescription);
            aiData.ServiceOverview = _transformationEngine.TransformDescription(aiData.ServiceOverview);
            
            // Transform capabilities
            if (aiData.Capabilities != null)
            {
                for (int i = 0; i < aiData.Capabilities.Count; i++)
                {
                    aiData.Capabilities[i] = _transformationEngine.TransformDescription(aiData.Capabilities[i]);
                }
            }
            
            // Transform prerequisites
            if (aiData.ServiceSpecificPrerequisites != null)
            {
                foreach (var prereq in aiData.ServiceSpecificPrerequisites)
                {
                    prereq.Description = _transformationEngine.TransformDescription(prereq.Description);
                }
            }
            
            // Transform tool descriptions
            if (aiData.Tools != null)
            {
                foreach (var tool in aiData.Tools)
                {
                    tool.ShortDescription = _transformationEngine.TransformDescription(tool.ShortDescription);
                }
            }
            
            // Transform scenarios
            if (aiData.Scenarios != null)
            {
                foreach (var scenario in aiData.Scenarios)
                {
                    scenario.Title = _transformationEngine.TransformDescription(scenario.Title);
                    scenario.Description = _transformationEngine.TransformDescription(scenario.Description);
                    scenario.ExpectedOutcome = _transformationEngine.TransformDescription(scenario.ExpectedOutcome);
                    if (scenario.Examples != null)
                    {
                        for (int i = 0; i < scenario.Examples.Count; i++)
                        {
                            scenario.Examples[i] = _transformationEngine.TransformDescription(scenario.Examples[i]);
                        }
                    }
                }
            }
            
            // Transform AI-specific scenarios
            if (aiData.AISpecificScenarios != null)
            {
                foreach (var scenario in aiData.AISpecificScenarios)
                {
                    scenario.Title = _transformationEngine.TransformDescription(scenario.Title);
                    scenario.Description = _transformationEngine.TransformDescription(scenario.Description);
                    if (scenario.Examples != null)
                    {
                        for (int i = 0; i < scenario.Examples.Count; i++)
                        {
                            scenario.Examples[i] = _transformationEngine.TransformDescription(scenario.Examples[i]);
                        }
                    }
                }
            }
            
            // Transform best practices
            if (aiData.BestPractices != null)
            {
                foreach (var practice in aiData.BestPractices)
                {
                    practice.Title = _transformationEngine.TransformDescription(practice.Title);
                    practice.Description = _transformationEngine.TransformDescription(practice.Description);
                }
            }
            
            // Transform common issues
            if (aiData.CommonIssues != null)
            {
                foreach (var issue in aiData.CommonIssues)
                {
                    issue.Title = _transformationEngine.TransformDescription(issue.Title);
                    issue.Description = _transformationEngine.TransformDescription(issue.Description);
                    issue.Resolution = _transformationEngine.TransformDescription(issue.Resolution);
                }
            }
            
            // Transform required roles
            if (aiData.RequiredRoles != null)
            {
                foreach (var role in aiData.RequiredRoles)
                {
                    role.Purpose = _transformationEngine.TransformDescription(role.Purpose);
                }
            }
            
            // Transform authentication notes
            if (!string.IsNullOrEmpty(aiData.AuthenticationNotes))
            {
                aiData.AuthenticationNotes = _transformationEngine.TransformDescription(aiData.AuthenticationNotes);
            }
            
            Console.WriteLine("✓ Text transformations applied");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to apply text transformations: {ex.Message}");
            // Continue without transformations
        }
    }

    /// <summary>
    /// Validate and auto-correct AI-generated content for common quality issues
    /// </summary>
    private void ValidateAIContent(AIGeneratedArticleData aiData, string serviceName, string progress)
    {
        var warnings = new List<string>();
        var corrections = new List<string>();

        // Check 1: Period before lowercase word (grammar error) - AUTO-FIX
        if (!string.IsNullOrEmpty(aiData.ServiceShortDescription))
        {
            var before = aiData.ServiceShortDescription;
            var after = System.Text.RegularExpressions.Regex.Replace(before, @"\. ([a-z])", " $1");
            if (after != before)
            {
                aiData.ServiceShortDescription = after;
                corrections.Add($"Fixed grammar in serviceShortDescription");
            }
        }
        
        if (!string.IsNullOrEmpty(aiData.ServiceOverview))
        {
            var before = aiData.ServiceOverview;
            var after = System.Text.RegularExpressions.Regex.Replace(before, @"\. ([a-z])", " $1");
            if (after != before)
            {
                aiData.ServiceOverview = after;
                corrections.Add($"Fixed grammar in serviceOverview");
            }
        }
        
        if (!string.IsNullOrEmpty(aiData.AuthenticationNotes))
        {
            var before = aiData.AuthenticationNotes;
            var after = System.Text.RegularExpressions.Regex.Replace(before, @"\. ([a-z])", " $1");
            if (after != before)
            {
                aiData.AuthenticationNotes = after;
                corrections.Add($"Fixed grammar in authenticationNotes");
            }
        }

        // Check 2: Redundant words at sentence start - AUTO-FIX
        if (!string.IsNullOrEmpty(aiData.ServiceOverview))
        {
            var words = aiData.ServiceOverview.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2 && words[0].Equals(words[1], StringComparison.OrdinalIgnoreCase))
            {
                // Remove the first redundant word
                aiData.ServiceOverview = string.Join(" ", words.Skip(1));
                corrections.Add($"Removed redundant word at start: '{words[0]}'");
            }
        }

        // Check 3: Invented RBAC roles - CRITICAL ERROR (BLOCKING)
        var knownAzureRoleKeywords = new[] 
        { 
            "Contributor", "Reader", "Owner", "User Access Administrator",
            "Data Reader", "Data Contributor", "Data Owner",
            "Index Data Reader", "Index Data Contributor", "Service Contributor",
            "Secrets User", "Secrets Officer", "Crypto Officer"
        };
        
        var suspiciousPatterns = new[] { "Knowledge Base Data", "Feature Data", "Resource Data", "KB Data" };
        var criticalErrors = new List<string>();
        
        if (aiData.RequiredRoles != null)
        {
            foreach (var role in aiData.RequiredRoles)
            {
                bool hasKnownKeyword = knownAzureRoleKeywords.Any(kw => role.Name.Contains(kw, StringComparison.OrdinalIgnoreCase));
                bool hasSuspiciousPattern = suspiciousPatterns.Any(sp => role.Name.Contains(sp, StringComparison.OrdinalIgnoreCase));
                
                if (hasSuspiciousPattern && !hasKnownKeyword)
                {
                    criticalErrors.Add($"❌ INVENTED RBAC ROLE: '{role.Name}' - Not in official Azure roles. Use official pattern like 'Search Index Data Reader' or 'Search Service Contributor'. Reference: https://learn.microsoft.com/en-us/azure/role-based-access-control/built-in-roles");
                }
            }
        }
        
        if (criticalErrors.Count > 0)
        {
            Console.WriteLine($"{progress} 🚫 CRITICAL VALIDATION ERRORS:");
            foreach (var error in criticalErrors)
            {
                Console.WriteLine($"    {error}");
            }
            throw new InvalidOperationException($"Critical validation failed for {serviceName}: RBAC roles must be from official Azure roles");
        }

        // Check 4: Tool descriptions quality
        if (aiData.Tools != null)
        {
            foreach (var tool in aiData.Tools)
            {
                var wordCount = tool.ShortDescription.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                if (wordCount < 6)
                {
                    warnings.Add($"⚠️  Tool '{tool.Command}' description too short: {wordCount} words (target: 8-12)");
                }
                if (tool.ShortDescription.Contains("get details", StringComparison.OrdinalIgnoreCase) ||
                    tool.ShortDescription.Contains("get information", StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add($"⚠️  Tool '{tool.Command}' has generic description: '{tool.ShortDescription}'");
                }
            }
        }

        // Check 5: Insufficient best practices
        if (aiData.BestPractices == null || aiData.BestPractices.Count < 4)
        {
            warnings.Add($"⚠️  Only {aiData.BestPractices?.Count ?? 0} best practices (minimum 4 required)");
        }

        // Output corrections
        if (corrections.Count > 0)
        {
            Console.WriteLine($"{progress} ✏️  Auto-corrections applied:");
            foreach (var correction in corrections)
            {
                Console.WriteLine($"    ✓ {correction}");
            }
        }

        // Output warnings
        if (warnings.Count > 0)
        {
            Console.WriteLine($"{progress} ⚠️  Quality warnings for {serviceName}:");
            foreach (var warning in warnings)
            {
                Console.WriteLine($"    {warning}");
            }
        }
    }

}

