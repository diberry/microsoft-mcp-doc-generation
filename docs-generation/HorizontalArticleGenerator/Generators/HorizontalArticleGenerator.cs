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
            AIGeneratedArticleData aiData = null;
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
            if (parseFailed) return false; // Skip this article
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
    private const string SYSTEM_PROMPT_PATH = "./prompts/horizontal-article-system-prompt.txt";
    private const string USER_PROMPT_PATH = "./prompts/horizontal-article-user-prompt.txt";
    private const string TEMPLATE_PATH = "./templates/horizontal-article-template.hbs";
    
    private readonly GenerativeAIClient _aiClient;
    private readonly bool _useTextTransformation;
    private readonly bool _generateAllArticles;

    public HorizontalArticleGenerator(GenerativeAIOptions options, bool useTextTransformation = false, bool generateAllArticles = false)
    {
        if (string.IsNullOrEmpty(options.ApiKey)) throw new InvalidOperationException("FOUNDRY_API_KEY not set");
        if (string.IsNullOrEmpty(options.Endpoint)) throw new InvalidOperationException("FOUNDRY_ENDPOINT not set");
        if (string.IsNullOrEmpty(options.Deployment)) throw new InvalidOperationException("FOUNDRY_MODEL_NAME not set");
        if (string.IsNullOrEmpty(options.ApiVersion)) throw new InvalidOperationException("FOUNDRY_MODEL_API_VERSION not set");
        _aiClient = new GenerativeAIClient(options);
        _useTextTransformation = useTextTransformation;
        _generateAllArticles = generateAllArticles;
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
                // Rate limiting
                if (i < staticDataList.Count - 1)
                {
                    await Task.Delay(1000);
                }
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

        // Load version info
        var versionPath = Path.GetFullPath(CLI_VERSION_PATH);
        var versionContent = await File.ReadAllTextAsync(versionPath);
        var versionJson = JsonDocument.Parse(versionContent);
        var cliVersion = versionJson.RootElement.GetProperty("version").GetString() ?? "unknown";

        // Group tools by service area (first word of command or name)
        var toolsByService = cliData.Results
            .Where(tool => !string.IsNullOrEmpty(tool.Command ?? tool.Name))
            .GroupBy(tool => (tool.Command ?? tool.Name)!.Split(' ')[0])
            .ToDictionary(g => g.Key, g => g.ToList());

        if (_useTextTransformation)
        {
            // Load brand mappings using existing infrastructure
            var configPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "transformation-config.json");
            var loader = new ConfigLoader(configPath);
            var config = await loader.LoadAsync();
            var engine = new TransformationEngine(config);

            foreach (var (serviceArea, tools) in toolsByService)
            {
                // Get brand name from transformation config
                var brandMapping = engine.Config.Services.Mappings
                    .FirstOrDefault(m => m.McpName == serviceArea);

                var staticData = new StaticArticleData
                {
                    ServiceBrandName = brandMapping?.BrandName ?? FormatServiceName(serviceArea),
                    ServiceIdentifier = serviceArea,
                    GeneratedAt = DateTime.UtcNow.ToString("o"),
                    Version = cliVersion,
                    ToolsReferenceLink = $"../tools/{brandMapping?.Filename ?? serviceArea}.md",
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
            // Use tool family/category name for filename and brand name
            foreach (var (serviceArea, tools) in toolsByService)
            {
                var staticData = new StaticArticleData
                {
                    ServiceBrandName = FormatServiceName(serviceArea),
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

        // Print prompts for debugging
        Console.WriteLine("--- System Prompt ---");
        Console.WriteLine(systemPrompt);
        Console.WriteLine("--- End System Prompt ---\n");
        Console.WriteLine("--- User Prompt ---");
        Console.WriteLine(userPrompt);
        Console.WriteLine("--- End User Prompt ---\n");

        // Call AI client
        var response = await _aiClient.GetChatCompletionAsync(
            systemPrompt,
            userPrompt
        );

        // Print prompts for debugging
        Console.WriteLine("--- System Prompt ---");
        Console.WriteLine(systemPrompt);
        Console.WriteLine("--- End System Prompt ---\n");
        Console.WriteLine("--- User Prompt ---");
        Console.WriteLine(userPrompt);
        Console.WriteLine("--- End User Prompt ---\n");

        // Print raw response
        Console.WriteLine("--- Raw GenerativeAI Response ---");
        Console.WriteLine(response);
        Console.WriteLine("--- End Raw Response ---\n");
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
        
        // Convert to dictionary for Handlebars
        var json = JsonSerializer.Serialize(templateData, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json)
            ?? throw new InvalidOperationException("Failed to convert template data");
        
        var renderedContent = await HandlebarsTemplateEngine.ProcessTemplateAsync(templatePath, data);
        
        // Save to file
        var outputDir = Path.GetFullPath(OUTPUT_DIR);
        var filename = $"horizontal-article-{templateData.ServiceIdentifier}.md";
        var outputPath = Path.Combine(outputDir, filename);
        
        await File.WriteAllTextAsync(outputPath, renderedContent);
    }
}
