// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using GenerativeAI;
using CSharpGenerator.Models;
using HorizontalArticleGenerator.Builders;
using HorizontalArticleGenerator.Models;
using TemplateEngine;
using Shared;
using Azure.Mcp.TextTransformation.Models;
using Azure.Mcp.TextTransformation.Services;
using System.Text;

namespace HorizontalArticleGenerator.Generators;

/// <summary>
/// Generates horizontal how-to articles for Azure services using AI content generation.
/// Extracts static data from CLI output, generates AI content, merges, and renders templates.
/// </summary>
public class HorizontalArticleGenerator
{
    /// <summary>
    /// Calculates the maximum token budget for AI article generation based on tool count.
    /// Base: 4000 tokens for article structure (sections, intro, prerequisites, RBAC, best practices).
    /// Plus: 600 tokens per tool (for tool descriptions, scenarios, etc.).
    /// Min: 8000 (even small namespaces need substantial output), Max: 24000.
    /// </summary>
    internal static int CalculateMaxTokens(int toolCount)
    {
        var calculatedTokens = 4000 + (toolCount * 600);
        return Math.Clamp(calculatedTokens, 8000, 24000);
    }

    /// <summary>
    /// Calculates the maximum token budget for per-tool AI calls or namespace summary calls.
    /// Per-tool calls are small and bounded (~2000 tokens each).
    /// Namespace summary calls use a compact tool list (command + description only), so a lower cap applies.
    /// </summary>
    internal static int CalculateMaxTokens(int toolCount, bool isPerToolCall)
    {
        if (isPerToolCall) return 2000;
        // Namespace summary: compact list only; lower base and per-tool factor than full article
        var calculatedTokens = 2000 + (toolCount * 150);
        return Math.Clamp(calculatedTokens, 3000, 8000);
    }

    // Extracted method for generating a single article
    private async Task<bool> GenerateSingleArticleAsync(StaticArticleData staticData, string outputDir, string progress)
    {
        try
        {
            Console.WriteLine($"{progress} Processing {staticData.ServiceBrandName}...");

            AIGeneratedArticleData? aiData = null;

            // Use per-tool + namespace-summary approach when new prompt files are present.
            // Fall back to legacy single-call if Sage's prompt files haven't landed yet.
            var toolSystemPromptPath = Path.GetFullPath(TOOL_SYSTEM_PROMPT_PATH);
            var toolUserPromptPath   = Path.GetFullPath(TOOL_USER_PROMPT_PATH);
            var namespaceUserPromptPath = Path.GetFullPath(NAMESPACE_USER_PROMPT_PATH);

            if (!File.Exists(toolSystemPromptPath) || !File.Exists(toolUserPromptPath) || !File.Exists(namespaceUserPromptPath))
            {
                Console.WriteLine($"{progress} ⚠️  Per-tool prompt files not found — using legacy single-call AI generation.");
                string aiResponse;
#pragma warning disable CS0618
                aiResponse = await GenerateAIContent(staticData);
#pragma warning restore CS0618
                try
                {
                    aiData = ParseAIResponse(aiResponse);
                }
                catch (Exception jsonEx)
                {
                    Console.WriteLine($"{progress} ✗ Failed to parse AI response for {staticData.ServiceBrandName}: {jsonEx.Message}");
                    var errorLog = Path.Combine(outputDir, $"error-{staticData.ServiceIdentifier}-airesponse.txt");
                    await File.WriteAllTextAsync(errorLog, $"Raw AI response:\n{aiResponse}\n\nError: {jsonEx.Message}\n{jsonEx.StackTrace}", Encoding.UTF8);
                    Console.WriteLine($"Raw AI response logged to: {errorLog}");
                    Console.WriteLine();
                    return false;
                }
            }
            else
            {
                // Per-tool AI calls: one per tool, bounded token budget (~2000 tokens each)
                Console.WriteLine($"{progress} Generating per-tool AI content ({staticData.Tools.Count} tools)...");
                var perToolResults = new List<PerToolAIData>();
                for (int i = 0; i < staticData.Tools.Count; i++)
                {
                    var tool = staticData.Tools[i];
                    Console.WriteLine($"{progress} ({i + 1}/{staticData.Tools.Count}) Tool: {tool.Command}");
                    var perToolData = await GenerateAIContentForTool(tool, staticData.ServiceBrandName, staticData.ServiceIdentifier, i);
                    perToolResults.Add(perToolData);
                }

                // Namespace summary AI call: one call after all per-tool calls, compact tool list
                Console.WriteLine($"{progress} Generating namespace summary...");
                var summaryData = await GenerateNamespaceSummaryAIContent(staticData, perToolResults);
                aiData = AggregateAIData(staticData, perToolResults, summaryData);

                // Fail fast if namespace summary returned empty required fields (indicates failed AI call)
                if (string.IsNullOrWhiteSpace(aiData.ServiceShortDescription) || string.IsNullOrWhiteSpace(aiData.ServiceOverview))
                {
                    Console.WriteLine($"{progress} ✗ Namespace summary returned empty required fields for {staticData.ServiceBrandName} — article generation failed.");
                    return false;
                }
            }

            if (aiData == null) return false;

            // Validate and transform AI-generated content via ArticleContentProcessor
            var processor = new ArticleContentProcessor(_transformationEngine);
            var validationResult = processor.Process(aiData, staticData.ServiceBrandName, staticData.ServiceIdentifier);

            // Output corrections
            if (validationResult.Corrections.Count > 0)
            {
                Console.WriteLine($"{progress} ✏️  Auto-corrections applied:");
                foreach (var correction in validationResult.Corrections)
                    Console.WriteLine($"    ✓ {correction}");
            }

            // Output warnings
            if (validationResult.Warnings.Count > 0)
            {
                Console.WriteLine($"{progress} ⚠️  Quality warnings for {staticData.ServiceBrandName}:");
                foreach (var warning in validationResult.Warnings)
                    Console.WriteLine($"    {warning}");
            }

            // Block on critical errors
            if (validationResult.HasCriticalErrors)
            {
                Console.WriteLine($"{progress} 🚫 CRITICAL VALIDATION ERRORS:");
                foreach (var error in validationResult.CriticalErrors)
                    Console.WriteLine($"    ❌ {error}");
                Console.WriteLine($"{progress} ✗ Validation failed for {staticData.ServiceBrandName}");
                Console.WriteLine();
                return false;
            }
            
            // Merge static + AI data
            var templateData = MergeData(staticData, aiData);
            
            // Load skills from Step 5 output (warn-only, never blocks)
            templateData.Skills = SkillsRelevanceReader.LoadRelevantSkills(_outputBasePath, staticData.ServiceIdentifier);
            
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
            await File.WriteAllTextAsync(errorLog, $"{ex.Message}\n\n{ex.StackTrace}", Encoding.UTF8);
            Console.WriteLine();
            return false;
        }
    }
    private static string DefaultOutputBase => Path.GetFullPath("../generated");
    private readonly string _cliOutputPath;
    private readonly string _outputDir;
    private readonly string _promptOutputDir;
    private readonly string _outputBasePath;
    private const string SYSTEM_PROMPT_PATH = "./DocGeneration.Steps.HorizontalArticles/prompts/horizontal-article-system-prompt.txt";
    private const string USER_PROMPT_PATH = "./DocGeneration.Steps.HorizontalArticles/prompts/horizontal-article-user-prompt.txt";
    private const string TEMPLATE_PATH = "./DocGeneration.Steps.HorizontalArticles/templates/horizontal-article-template.hbs";

    // Per-tool AI call prompts (written by Sage; may not exist yet — generator falls back gracefully)
    private const string TOOL_SYSTEM_PROMPT_PATH = "./DocGeneration.Steps.HorizontalArticles/prompts/horizontal-article-tool-system-prompt.txt";
    private const string TOOL_USER_PROMPT_PATH = "./DocGeneration.Steps.HorizontalArticles/prompts/horizontal-article-tool-user-prompt.txt";
    private const string NAMESPACE_USER_PROMPT_PATH = "./DocGeneration.Steps.HorizontalArticles/prompts/horizontal-article-namespace-user-prompt.txt";
    
    private readonly GenerativeAIClient _aiClient;
    private readonly bool _useTextTransformation;
    private readonly bool _generateAllArticles;
    private readonly TransformationEngine? _transformationEngine;

    public HorizontalArticleGenerator(GenerativeAIOptions options, bool useTextTransformation = false, bool generateAllArticles = false, TransformationEngine? transformationEngine = null, string? outputBasePath = null)
    {
        if (string.IsNullOrEmpty(options.ApiKey)) throw new InvalidOperationException("FOUNDRY_API_KEY not set");
        if (string.IsNullOrEmpty(options.Endpoint)) throw new InvalidOperationException("FOUNDRY_ENDPOINT not set");
        if (string.IsNullOrEmpty(options.Deployment)) throw new InvalidOperationException("FOUNDRY_MODEL_NAME not set");
        if (string.IsNullOrEmpty(options.ApiVersion)) throw new InvalidOperationException("FOUNDRY_MODEL_API_VERSION not set");
        _aiClient = new GenerativeAIClient(options);
        _useTextTransformation = useTextTransformation;
        _generateAllArticles = generateAllArticles;
        _transformationEngine = transformationEngine;
        _outputBasePath = outputBasePath != null ? Path.GetFullPath(outputBasePath) : DefaultOutputBase;
        _cliOutputPath = Path.Combine(_outputBasePath, "cli", "cli-output.json");
        _outputDir = Path.Combine(_outputBasePath, "horizontal-articles");
        _promptOutputDir = Path.Combine(_outputBasePath, "horizontal-article-prompts");
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
        Directory.CreateDirectory(_outputDir);
        Console.WriteLine($"Output directory: {_outputDir}");
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
                bool result = await GenerateSingleArticleAsync(staticData, _outputDir, progress);
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
                bool result = await GenerateSingleArticleAsync(staticData, _outputDir, progress);
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
        var outlineBuilder = new ArticleOutlineBuilder();
        var outline = await outlineBuilder.BuildAsync(_outputBasePath, serviceArea, CancellationToken.None);
        var hasToolEvidence = outline.Sections
            .FirstOrDefault(section => section.Heading == "Tool overview")?
            .EvidenceItems
            .Any() ?? false;
        if (!hasToolEvidence)
        {
            Console.Error.WriteLine($"✗ Service not found: {serviceArea}");
            return;
        }

        Directory.CreateDirectory(_outputDir);
        bool result = await GenerateSingleArticleAsync(outline, _outputDir, "[1/1]", CancellationToken.None);

        if (!result)
        {
            Console.Error.WriteLine($"✗ Single service article generation failed for {serviceArea}");
        }
    }

    public async Task<string> GenerateArticleMarkdownAsync(ArticleOutlineContext outlineContext, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var staticData = await BuildStaticArticleDataAsync(outlineContext, cancellationToken);
#pragma warning disable CS0618
        var aiResponse = await GenerateAIContent(staticData);
#pragma warning restore CS0618
        var aiData = ParseAIResponse(aiResponse);
        var processor = new ArticleContentProcessor(_transformationEngine);
        var validationResult = processor.Process(aiData, staticData.ServiceBrandName, staticData.ServiceIdentifier);
        if (validationResult.HasCriticalErrors)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validationResult.CriticalErrors));
        }

        var templateData = MergeData(staticData, aiData);
        templateData.Skills = SkillsRelevanceReader.LoadRelevantSkills(_outputBasePath, staticData.ServiceIdentifier);
        return await RenderArticleAsync(templateData);
    }

    /// <summary>
    /// Extracts static data for all services from CLI output and transformation config
    /// </summary>
    private async Task<List<StaticArticleData>> ExtractStaticData()
    {
        var serviceDataList = new List<StaticArticleData>();

        // Load CLI output
        var cliOutputPath = _cliOutputPath;
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
        var cliVersion = await CliVersionReader.ReadCliVersionAsync(_outputBasePath);

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
                if (transformMapping?.BrandName != null)
                {
                    serviceBrandName = transformMapping.BrandName;
                }
                else if (sharedBrandMappings.TryGetValue(serviceArea, out var brandMap))
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
                    ToolsReferenceLink = BuildToolsReferenceLink(serviceArea),
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
                    ToolsReferenceLink = BuildToolsReferenceLink(serviceArea),
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
    
    private static string BuildToolsReferenceLink(string serviceArea)
    {
        return $"../tool-family/{serviceArea}.md";
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
    [Obsolete("Use GenerateAIContentForTool + GenerateNamespaceSummaryAIContent instead. This single-call method causes token overflow on large namespaces.")]
    private async Task<string> GenerateAIContent(StaticArticleData staticData)
    {
        // Load prompts
        var systemPromptPath = Path.GetFullPath(SYSTEM_PROMPT_PATH);
        var userPromptPath = Path.GetFullPath(USER_PROMPT_PATH);
        
        var systemPrompt = await File.ReadAllTextAsync(systemPromptPath);
        systemPrompt = PromptTokenResolver.Resolve(systemPrompt, Path.Combine(AppContext.BaseDirectory, "data"));
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
        Directory.CreateDirectory(_promptOutputDir);
        
        var promptFileName = $"horizontal-article-{staticData.ServiceIdentifier}-prompt.md";
        var promptFilePath = Path.Combine(_promptOutputDir, promptFileName);
        var promptContent = $"""
# Horizontal Article Prompt: {staticData.ServiceBrandName}

Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

## System Prompt

{systemPrompt}

## User Prompt

{userPrompt}
""";
        await File.WriteAllTextAsync(promptFilePath, promptContent, Encoding.UTF8);

        // Calculate token limit based on tool count
        var maxTokens = CalculateMaxTokens(staticData.Tools.Count);

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
    /// Calls AI once for a single tool to generate its short description, scenario, and capability.
    /// Returns a static-data fallback if prompt files are not present (Sage may not have written them yet).
    /// </summary>
    private async Task<PerToolAIData> GenerateAIContentForTool(
        HorizontalToolSummary tool,
        string serviceBrandName,
        string serviceIdentifier,
        int toolIndex)
    {
        var systemPromptPath = Path.GetFullPath(TOOL_SYSTEM_PROMPT_PATH);
        var userPromptPath   = Path.GetFullPath(TOOL_USER_PROMPT_PATH);

        if (!File.Exists(systemPromptPath) || !File.Exists(userPromptPath))
        {
            Console.WriteLine($"    ⚠️  Per-tool prompt files not found; using static description as fallback for: {tool.Command}");
            return new PerToolAIData { Command = tool.Command, ShortDescription = tool.Description };
        }

        try
        {
            var systemPrompt = await File.ReadAllTextAsync(systemPromptPath);
            systemPrompt = PromptTokenResolver.Resolve(systemPrompt, Path.Combine(AppContext.BaseDirectory, "data"));
            var userPromptTemplate = await File.ReadAllTextAsync(userPromptPath);

            var handlebars = HandlebarsDotNet.Handlebars.Create();
            var userPromptCompiled = handlebars.Compile(userPromptTemplate);

            var promptContext = new
            {
                serviceBrandName,
                serviceIdentifier,
                tool = new
                {
                    command = tool.Command,
                    description = tool.Description,
                    parameterCount = tool.ParameterCount,
                    metadata = new
                    {
                        destructive = new { value = tool.Metadata.GetValueOrDefault("destructive", new MetadataValue()).Value },
                        readOnly    = new { value = tool.Metadata.GetValueOrDefault("readOnly", new MetadataValue()).Value },
                        secret      = new { value = tool.Metadata.GetValueOrDefault("secret", new MetadataValue()).Value }
                    }
                }
            };

            var userPrompt = userPromptCompiled(promptContext);

            // Save prompt for debugging
            Directory.CreateDirectory(_promptOutputDir);
            var promptFileName = $"horizontal-article-{serviceIdentifier}-tool-{toolIndex:D2}-prompt.md";
            var promptFilePath = Path.Combine(_promptOutputDir, promptFileName);
            var promptContent = $"""
# Per-Tool Prompt: {tool.Command} ({serviceBrandName})

Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

## System Prompt

{systemPrompt}

## User Prompt

{userPrompt}
""";
            await File.WriteAllTextAsync(promptFilePath, promptContent, Encoding.UTF8);

            var maxTokens = CalculateMaxTokens(1, isPerToolCall: true);
            var response = await _aiClient.GetChatCompletionAsync(systemPrompt, userPrompt, maxTokens);

            await File.AppendAllTextAsync(promptFilePath, $"""


## AI Response

```json
{response}
```
""");

            var jsonContent = ExtractJsonFromResponse(response);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<PerToolAIData>(jsonContent, options)
                ?? new PerToolAIData { Command = tool.Command, ShortDescription = tool.Description };
            result.Command = tool.Command; // Always set — JSON won't include it
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ⚠️  AI call failed for tool {tool.Command}: {ex.Message} — using static fallback.");
            return new PerToolAIData { Command = tool.Command, ShortDescription = tool.Description };
        }
    }

    /// <summary>
    /// Calls AI once for the namespace-level summary after all per-tool calls complete.
    /// Input is a compact list (command + description only) to keep the token count low.
    /// Returns an empty summary if the namespace user prompt file is not present.
    /// </summary>
    private async Task<NamespaceSummaryAIData> GenerateNamespaceSummaryAIContent(
        StaticArticleData staticData,
        IReadOnlyList<PerToolAIData> perToolResults)
    {
        var systemPromptPath = Path.GetFullPath(SYSTEM_PROMPT_PATH);
        var userPromptPath   = Path.GetFullPath(NAMESPACE_USER_PROMPT_PATH);

        if (!File.Exists(userPromptPath))
        {
            Console.WriteLine($"    ⚠️  Namespace summary user prompt not found; using empty summary for: {staticData.ServiceBrandName}");
            return new NamespaceSummaryAIData();
        }

        try
        {
            var systemPrompt = await File.ReadAllTextAsync(systemPromptPath);
            systemPrompt = PromptTokenResolver.Resolve(systemPrompt, Path.Combine(AppContext.BaseDirectory, "data"));
            var userPromptTemplate = await File.ReadAllTextAsync(userPromptPath);

            var handlebars = HandlebarsDotNet.Handlebars.Create();
            var userPromptCompiled = handlebars.Compile(userPromptTemplate);

            // Compact tool list: only command + description to stay within token budget
            var promptContext = new
            {
                serviceBrandName   = staticData.ServiceBrandName,
                serviceIdentifier  = staticData.ServiceIdentifier,
                toolsReferenceLink = staticData.ToolsReferenceLink,
                tools = staticData.Tools.Select(t => new { command = t.Command, description = t.Description })
            };

            var userPrompt = userPromptCompiled(promptContext);

            // Save prompt for debugging
            Directory.CreateDirectory(_promptOutputDir);
            var promptFileName = $"horizontal-article-{staticData.ServiceIdentifier}-namespace-prompt.md";
            var promptFilePath = Path.Combine(_promptOutputDir, promptFileName);
            var promptContent = $"""
# Namespace Summary Prompt: {staticData.ServiceBrandName}

Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

## System Prompt

{systemPrompt}

## User Prompt

{userPrompt}
""";
            await File.WriteAllTextAsync(promptFilePath, promptContent, Encoding.UTF8);

            var maxTokens = CalculateMaxTokens(staticData.Tools.Count, isPerToolCall: false);
            var response = await _aiClient.GetChatCompletionAsync(systemPrompt, userPrompt, maxTokens);

            await File.AppendAllTextAsync(promptFilePath, $"""


## AI Response

```json
{response}
```
""");

            var jsonContent = ExtractJsonFromResponse(response);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            return JsonSerializer.Deserialize<NamespaceSummaryAIData>(jsonContent, options)
                ?? new NamespaceSummaryAIData();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ⚠️  Namespace summary AI call failed for {staticData.ServiceBrandName}: {ex.Message} — using empty summary.");
            return new NamespaceSummaryAIData();
        }
    }

    /// <summary>
    /// Aggregates per-tool AI results and namespace summary into a single AIGeneratedArticleData
    /// for compatibility with the existing ArticleContentProcessor and MergeData pipeline.
    /// </summary>
    internal static AIGeneratedArticleData AggregateAIData(
        StaticArticleData staticData,
        IReadOnlyList<PerToolAIData> perToolResults,
        NamespaceSummaryAIData summaryData)
    {
        return new AIGeneratedArticleData
        {
            // Namespace-level fields from summary call
            ServiceShortDescription      = summaryData.ServiceShortDescription,
            ServiceOverview              = summaryData.ServiceOverview,
            ServiceSpecificPrerequisites = summaryData.ServiceSpecificPrerequisites,
            RequiredRoles                = summaryData.RequiredRoles,
            BestPractices                = summaryData.BestPractices,
            ServiceDocLink               = summaryData.ServiceDocLink,
            AdditionalLinks              = summaryData.AdditionalLinks,

            // Capabilities: one entry per tool
            Capabilities = perToolResults
                .Where(p => !string.IsNullOrWhiteSpace(p.Capability))
                .Select(p => p.Capability)
                .ToList(),

            // Scenarios: one scenario per tool that returned one
            Scenarios = perToolResults
                .Where(p => p.Scenario != null)
                .Select(p => p.Scenario!)
                .ToList(),

            // Per-tool short descriptions mapped to ToolWithAIDescription
            Tools = perToolResults.Select(p => new ToolWithAIDescription
            {
                Command          = p.Command,
                ShortDescription = p.ShortDescription,
                MoreInfoLink     = staticData.Tools
                    .FirstOrDefault(t => t.Command == p.Command)?.MoreInfoLink ?? string.Empty
            }).ToList()
        };
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
        // Merge tools: use AI-returned order (management plane first, then data plane)
        // Fall back to static order if AI tools are unavailable
        List<MergedTool> mergedTools;
        if (aiData.Tools?.Count > 0)
        {
            mergedTools = aiData.Tools.Select(aiTool =>
            {
                var staticTool = staticData.Tools.FirstOrDefault(
                    t => t.Command == aiTool.Command);
                
                return new MergedTool
                {
                    Command = aiTool.Command,
                    MoreInfoLink = staticTool?.MoreInfoLink ?? "",
                    ShortDescription = aiTool.ShortDescription
                };
            }).ToList();
            
            // Append any static tools not in the AI response
            foreach (var staticTool in staticData.Tools)
            {
                if (!mergedTools.Any(m => m.Command == staticTool.Command))
                {
                    mergedTools.Add(new MergedTool
                    {
                        Command = staticTool.Command,
                        MoreInfoLink = staticTool.MoreInfoLink,
                        ShortDescription = staticTool.Description
                    });
                }
            }
        }
        else
        {
            mergedTools = staticData.Tools.Select(staticTool => new MergedTool
            {
                Command = staticTool.Command,
                MoreInfoLink = staticTool.MoreInfoLink,
                ShortDescription = staticTool.Description
            }).ToList();
        }
        
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
        var filename = $"horizontal-article-{templateData.ServiceIdentifier}.md";
        var outputPath = Path.Combine(_outputDir, filename);
        var renderedContent = await RenderArticleAsync(templateData);
        await File.WriteAllTextAsync(outputPath, renderedContent, Encoding.UTF8);
    }

    private async Task<string> RenderArticleAsync(HorizontalArticleTemplateData templateData)
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
            ["genai-aiSpecificScenarios"] = templateData.AISpecificScenarios ?? (object)new List<AIScenario>(),
            ["genai-requiredRoles"] = templateData.RequiredRoles,
            ["genai-authenticationNotes"] = templateData.AuthenticationNotes ?? string.Empty,
            ["genai-commonIssues"] = templateData.CommonIssues ?? (object)new List<CommonIssue>(),
            ["genai-bestPractices"] = templateData.BestPractices ?? (object)new List<BestPractice>(),
            ["genai-serviceDocLink"] = templateData.ServiceDocLink ?? string.Empty,
            ["genai-additionalLinks"] = templateData.AdditionalLinks,
            
            // Merged tools - convert to dictionaries for Handlebars
            ["tools"] = templateData.Tools.Select(t => new Dictionary<string, object>
            {
                ["command"] = t.Command,
                ["moreInfoLink"] = t.MoreInfoLink,
                ["genai-shortDescription"] = t.ShortDescription
            }).ToList()
        };
        
        // Add skills data if available (from Step 5 output)
        if (templateData.Skills != null && templateData.Skills.Count > 0)
        {
            data["skills"] = templateData.Skills.Select(s => new Dictionary<string, object>
            {
                ["name"] = s.Name,
                ["description"] = s.Description,
                ["sourceUrl"] = s.SourceUrl
            }).ToList();
        }
        
        return await HandlebarsTemplateEngine.ProcessTemplateAsync(templatePath, data);
    }

    private async Task<bool> GenerateSingleArticleAsync(
        ArticleOutlineContext outlineContext,
        string outputDir,
        string progress,
        CancellationToken cancellationToken)
    {
        try
        {
            Console.WriteLine($"{progress} Processing {outlineContext.ArticleTitle}...");
            var renderedContent = await GenerateArticleMarkdownAsync(outlineContext, cancellationToken);
            Directory.CreateDirectory(outputDir);
            await File.WriteAllTextAsync(
                Path.Combine(outputDir, $"horizontal-article-{outlineContext.ServiceIdentifier}.md"),
                renderedContent,
                Encoding.UTF8,
                cancellationToken);
            Console.WriteLine($"{progress} ✓ Generated: horizontal-article-{outlineContext.ServiceIdentifier}.md");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{progress} ✗ Failed for {outlineContext.ArticleTitle}: {ex.Message}");
            var errorLog = Path.Combine(outputDir, $"error-{outlineContext.ServiceIdentifier}.txt");
            await File.WriteAllTextAsync(errorLog, $"{ex.Message}{Environment.NewLine}{Environment.NewLine}{ex.StackTrace}", Encoding.UTF8, cancellationToken);
            Console.WriteLine();
            return false;
        }
    }

    private async Task<StaticArticleData> BuildStaticArticleDataAsync(ArticleOutlineContext outlineContext, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cliVersion = await CliVersionReader.ReadCliVersionAsync(_outputBasePath);
        var tools = ExtractToolsFromOutline(outlineContext);

        return new StaticArticleData
        {
            ServiceBrandName = outlineContext.ArticleTitle,
            ServiceIdentifier = outlineContext.ServiceIdentifier,
            GeneratedAt = DateTime.UtcNow.ToString("o"),
            Version = cliVersion,
            ToolsReferenceLink = outlineContext.Sections
                .SelectMany(section => section.EvidenceItems)
                .FirstOrDefault(item => item.StartsWith("xref:../tool-family/", StringComparison.OrdinalIgnoreCase))?
                .Replace("xref:", string.Empty, StringComparison.OrdinalIgnoreCase)
                ?? $"../tool-family/{outlineContext.ServiceIdentifier}.md",
            Tools = tools
        };
    }

    private static List<HorizontalToolSummary> ExtractToolsFromOutline(ArticleOutlineContext outlineContext)
    {
        var toolOverviewSection = outlineContext.Sections.FirstOrDefault(section => section.Heading == "Tool overview");
        if (toolOverviewSection is null)
        {
            return [];
        }

        var tools = new List<HorizontalToolSummary>();
        foreach (var evidenceItem in toolOverviewSection.EvidenceItems)
        {
            using var document = JsonDocument.Parse(evidenceItem);
            var root = document.RootElement;
            if (!root.TryGetProperty("kind", out var kind) || !string.Equals(kind.GetString(), "tool", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            tools.Add(new HorizontalToolSummary
            {
                Command = root.GetProperty("command").GetString() ?? string.Empty,
                Description = root.GetProperty("description").GetString() ?? string.Empty,
                ParameterCount = root.TryGetProperty("parameterCount", out var parameterCount) ? parameterCount.GetInt32() : 0,
                MoreInfoLink = root.TryGetProperty("moreInfoLink", out var moreInfoLink) ? moreInfoLink.GetString() ?? string.Empty : string.Empty,
                Metadata = new Dictionary<string, MetadataValue>(StringComparer.OrdinalIgnoreCase)
                {
                    ["destructive"] = new MetadataValue { Value = root.TryGetProperty("destructive", out var destructive) && destructive.GetBoolean() },
                    ["readOnly"] = new MetadataValue { Value = root.TryGetProperty("readOnly", out var readOnly) && readOnly.GetBoolean() },
                    ["secret"] = new MetadataValue { Value = root.TryGetProperty("secret", out var secret) && secret.GetBoolean() }
                }
            });
        }

        return tools;
    }

}
