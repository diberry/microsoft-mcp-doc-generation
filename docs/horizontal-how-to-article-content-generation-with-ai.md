# Horizontal How-To Article Content Generation with AI

## Overview

This document outlines the plan for generating horizontal Azure service articles using AI to populate a Handlebars template. The process involves three phases:

1. **Extract static data** from existing generated content (CLI output, brand mappings)
2. **Generate AI content** using the GenerativeAI library with system/user prompts
3. **Merge and render** static + AI data through the Handlebars template

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ Phase 1: Static Data Extraction                                 │
│ ────────────────────────────────────────────────────────────── │
│ Input: cli-output.json, brand-to-server-mapping.json           │
│ Process: HorizontalArticleGenerator.ExtractStaticData()        │
│ Output: StaticArticleData per service                          │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ Phase 2: AI Content Generation                                  │
│ ────────────────────────────────────────────────────────────── │
│ Input: StaticArticleData + prompts                             │
│ Process: GenerativeAIClient.GenerateContentAsync()             │
│ Output: AIGeneratedArticleData (JSON)                          │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│ Phase 3: Template Rendering                                     │
│ ────────────────────────────────────────────────────────────── │
│ Input: StaticArticleData + AIGeneratedArticleData              │
│ Process: HandlebarsTemplateEngine.Render()                     │
│ Output: horizontal-article-{service}.md files                  │
└─────────────────────────────────────────────────────────────────┘
```

## Phase 1: Static Data Extraction

### Data Sources

**Already Generated/Available:**
- `generated/cli/cli-output.json` - All MCP tool definitions (16,635 lines, 181 tools)
- `docs-generation/brand-to-server-mapping.json` - Service brand names
- `docs-generation/compound-words.json` - Word transformations
- MCP CLI version info - From generation process

### Static Data Model

```csharp
// File: CSharpGenerator/Models/StaticArticleData.cs
public class StaticArticleData
{
    public string ServiceBrandName { get; set; }          // "Azure Storage"
    public string ServiceIdentifier { get; set; }         // "storage"
    public string GeneratedAt { get; set; }               // ISO 8601 timestamp
    public string Version { get; set; }                   // MCP CLI version
    public string ToolsReferenceLink { get; set; }        // Generated link
    public List<ToolSummary> Tools { get; set; }
}

public class ToolSummary
{
    public string Command { get; set; }                   // "storage account create"
    public string Description { get; set; }               // Full tool description
    public int ParameterCount { get; set; }               // Non-common params only
    public ToolMetadata Metadata { get; set; }
    public string MoreInfoLink { get; set; }              // Link to param reference
}

public class ToolMetadata
{
    public MetadataValue Destructive { get; set; }
    public MetadataValue ReadOnly { get; set; }
    public MetadataValue Secret { get; set; }
}

public class MetadataValue
{
    public bool Value { get; set; }
    public string Source { get; set; }
}
```

### Extraction Logic - REUSE EXISTING CODE

```csharp
// File: CSharpGenerator/Generators/HorizontalArticleGenerator.cs
// Reuses DocumentationGenerator's existing tool parsing logic
public class HorizontalArticleGenerator
{
    private const string CLI_OUTPUT_PATH = "../generated/cli/cli-output.json";
    private const string CLI_VERSION_PATH = "../generated/cli/cli-version.json";
    
    public async Task<List<StaticArticleData>> ExtractStaticData()
    {
        var serviceDataList = new List<StaticArticleData>();
        
        // Load CLI output using EXISTING parsing from DocumentationGenerator
        var cliOutputPath = Path.GetFullPath(CLI_OUTPUT_PATH);
        if (!File.Exists(cliOutputPath))
        {
            throw new FileNotFoundException($"CLI output not found: {cliOutputPath}");
        }
        
        var jsonContent = await File.ReadAllTextAsync(cliOutputPath);
        var cliData = JsonSerializer.Deserialize<CliOutput>(jsonContent);
        
        if (cliData?.Results == null)
        {
            throw new InvalidOperationException("Invalid CLI output format");
        }
        
        // Load version info
        var versionPath = Path.GetFullPath(CLI_VERSION_PATH);
        var versionData = JsonSerializer.Deserialize<CliVersion>(
            await File.ReadAllTextAsync(versionPath));
        var cliVersion = versionData?.Version ?? "unknown";
        
        // Group tools by service area (first word of command)
        // REUSES existing logic from DocumentationGenerator
        var toolsByService = cliData.Results
            .GroupBy(tool => tool.Name.Split(' ')[0])
            .ToDictionary(g => g.Key, g => g.ToList());
        
        // Load brand mappings using EXISTING infrastructure
        var engine = await DocumentationGenerator.GetTransformationEngineAsync();
        
        foreach (var (serviceArea, tools) in toolsByService)
        {
            // REUSE existing brand mapping logic
            var brandMapping = engine.Config.Services.Mappings
                .FirstOrDefault(m => m.McpName == serviceArea);
            
            var staticData = new StaticArticleData
            {
                ServiceBrandName = brandMapping?.BrandName ?? FormatServiceName(serviceArea),
                ServiceIdentifier = serviceArea,
                GeneratedAt = DateTime.UtcNow.ToString("o"),
                Version = cliVersion,
                ToolsReferenceLink = $"../tools/{brandMapping?.Filename ?? serviceArea}.md",
                Tools = tools.Select(tool => new ToolSummary
                {
                    Command = tool.Name,
                    Description = tool.Description,
                    ParameterCount = CountNonCommonParameters(tool),
                    Metadata = ExtractMetadata(tool),
                    MoreInfoLink = $"../parameters/{tool.Name.Replace(' ', '-')}-parameters.md"
                }).ToList()
            };
            
            serviceDataList.Add(staticData);
        }
        
        return serviceDataList;
    }
    
    private static int CountNonCommonParameters(CliTool tool)
    {
        // REUSE existing common parameters list from DocumentationGenerator
        var commonParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "subscription-id", "resource-group", "output", "verbose", 
            "help", "debug", "only-show-errors"
        };
        
        return tool.InputSchema?.Properties?.Count(p => 
            !commonParams.Contains(p.Key)) ?? 0;
    }
    
    private static string FormatServiceName(string serviceArea)
    {
        // Simple formatting - capitalize first letter
        return char.ToUpper(serviceArea[0]) + serviceArea.Substring(1);
    }
}
```

### Key Changes for Minimal Code Impact

1. **Reuse CliOutput Model**: Use existing `CliOutput`, `CliTool` models from DocumentationGenerator
2. **Reuse TransformationEngine**: Call `DocumentationGenerator.GetTransformationEngineAsync()` directly
3. **Reuse Brand Mappings**: Access existing transformation config instead of reading JSON again
4. **Reuse Common Parameters**: Same list DocumentationGenerator uses
5. **Simple Grouping**: Built-in LINQ instead of custom logic

## Phase 2: AI Content Generation with Prompts

### Prompt String Interpolation

The system and user prompts need to be populated with static data using string interpolation.

#### User Prompt Template Processing

```csharp
// File: CSharpGenerator/Generators/HorizontalArticleGenerator.cs (continued)
private async Task<string> GenerateAIContent(StaticArticleData staticData)
{
    // Load prompt templates
    var systemPromptPath = Path.Combine(_config.BasePath, "prompts", 
        "horizontal-article-system-prompt.txt");
    var userPromptPath = Path.Combine(_config.BasePath, "prompts", 
        "horizontal-article-user-prompt.txt");
    
    var systemPrompt = File.ReadAllText(systemPromptPath);
    var userPromptTemplate = File.ReadAllText(userPromptPath);
    
    // Process user prompt with Handlebars
    var handlebars = Handlebars.Create();
    var userPromptCompiled = handlebars.Compile(userPromptTemplate);
    
    // Create context object matching user prompt template
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
                destructive = new { value = t.Metadata.Destructive.Value },
                readOnly = new { value = t.Metadata.ReadOnly.Value },
                secret = new { value = t.Metadata.Secret.Value }
            }
        })
    };
    
    // Render user prompt with static data
    var userPrompt = userPromptCompiled(promptContext);
    
    // Call GenerativeAI client
    var aiClient = new GenerativeAIClient(new GenerativeAIOptions
    {
        ApiKey = Environment.GetEnvironmentVariable("FOUNDRY_API_KEY"),
        Endpoint = Environment.GetEnvironmentVariable("FOUNDRY_ENDPOINT"),
        ModelName = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_NAME"),
        ApiVersion = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_API_VERSION")
    });
    
    var aiResponse = await aiClient.GenerateContentAsync(
        systemPrompt: systemPrompt,
        userPrompt: userPrompt
    );
    
    return aiResponse; // JSON string
}
```

### AI Response Model

```csharp
// File: CSharpGenerator/Models/AIGeneratedArticleData.cs
public class AIGeneratedArticleData
{
    [JsonPropertyName("genai-serviceShortDescription")]
    public string ServiceShortDescription { get; set; }
    
    [JsonPropertyName("genai-serviceOverview")]
    public string ServiceOverview { get; set; }
    
    [JsonPropertyName("genai-capabilities")]
    public List<string> Capabilities { get; set; }
    
    [JsonPropertyName("genai-serviceSpecificPrerequisites")]
    public List<Prerequisite> ServiceSpecificPrerequisites { get; set; }
    
    public List<ToolWithAIDescription> Tools { get; set; }
    
    [JsonPropertyName("genai-scenarios")]
    public List<Scenario> Scenarios { get; set; }
    
    [JsonPropertyName("genai-aiSpecificScenarios")]
    public List<AIScenario> AISpecificScenarios { get; set; }
    
    [JsonPropertyName("genai-requiredRoles")]
    public List<RequiredRole> RequiredRoles { get; set; }
    
    [JsonPropertyName("genai-authenticationNotes")]
    public string AuthenticationNotes { get; set; }
    
    [JsonPropertyName("genai-commonIssues")]
    public List<CommonIssue> CommonIssues { get; set; }
    
    [JsonPropertyName("genai-bestPractices")]
    public List<BestPractice> BestPractices { get; set; }
    
    [JsonPropertyName("genai-serviceDocLink")]
    public string ServiceDocLink { get; set; }
    
    [JsonPropertyName("genai-additionalLinks")]
    public List<AdditionalLink> AdditionalLinks { get; set; }
}

public class Prerequisite
{
    public string Title { get; set; }
    public string Description { get; set; }
}

public class ToolWithAIDescription
{
    public string Command { get; set; }
    
    [JsonPropertyName("genai-shortDescription")]
    public string ShortDescription { get; set; }
    
    public string MoreInfoLink { get; set; }
}

public class Scenario
{
    public string Title { get; set; }
    public string Description { get; set; }
    public List<string> Examples { get; set; }
    public string ExpectedOutcome { get; set; }
}

public class AIScenario
{
    public string Title { get; set; }
    public string Description { get; set; }
    public List<string> Examples { get; set; }
}

public class RequiredRole
{
    public string Name { get; set; }
    public string Purpose { get; set; }
}

public class CommonIssue
{
    public string Title { get; set; }
    public string Description { get; set; }
    public string Resolution { get; set; }
}

public class BestPractice
{
    public string Title { get; set; }
    public string Description { get; set; }
}

public class AdditionalLink
{
    public string Title { get; set; }
    public string Url { get; set; }
}
```

### Parsing AI Response

```csharp
// File: CSharpGenerator/Generators/HorizontalArticleGenerator.cs (continued)
private AIGeneratedArticleData ParseAIResponse(string aiResponse)
{
    // AI should return pure JSON, but may wrap in markdown code blocks
    var jsonContent = ExtractJsonFromResponse(aiResponse);
    
    var options = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    return JsonSerializer.Deserialize<AIGeneratedArticleData>(jsonContent, options);
}

private string ExtractJsonFromResponse(string response)
{
    // Remove markdown code blocks if present
    var trimmed = response.Trim();
    
    if (trimmed.StartsWith("```json"))
    {
        trimmed = trimmed.Substring(7); // Remove ```json
        var endIndex = trimmed.LastIndexOf("```");
        if (endIndex > 0)
            trimmed = trimmed.Substring(0, endIndex);
    }
    else if (trimmed.StartsWith("```"))
    {
        trimmed = trimmed.Substring(3); // Remove ```
        var endIndex = trimmed.LastIndexOf("```");
        if (endIndex > 0)
            trimmed = trimmed.Substring(0, endIndex);
    }
    
    return trimmed.Trim();
}
```

## Phase 3: Merge and Render

### Combined Data Model for Template

```csharp
// File: CSharpGenerator/Models/HorizontalArticleTemplateData.cs
public class HorizontalArticleTemplateData
{
    // Static fields (from Phase 1)
    public string ServiceBrandName { get; set; }
    public string ServiceIdentifier { get; set; }
    public string GeneratedAt { get; set; }
    public string Version { get; set; }
    public string ToolsReferenceLink { get; set; }
    
    // AI-generated fields (from Phase 2) - with genai- prefix in template
    [JsonPropertyName("genai-serviceShortDescription")]
    public string ServiceShortDescription { get; set; }
    
    [JsonPropertyName("genai-serviceOverview")]
    public string ServiceOverview { get; set; }
    
    [JsonPropertyName("genai-capabilities")]
    public List<string> Capabilities { get; set; }
    
    [JsonPropertyName("genai-serviceSpecificPrerequisites")]
    public List<Prerequisite> ServiceSpecificPrerequisites { get; set; }
    
    // Merged tools (static command/moreInfoLink + AI shortDescription)
    public List<MergedTool> Tools { get; set; }
    
    [JsonPropertyName("genai-scenarios")]
    public List<Scenario> Scenarios { get; set; }
    
    [JsonPropertyName("genai-aiSpecificScenarios")]
    public List<AIScenario> AISpecificScenarios { get; set; }
    
    [JsonPropertyName("genai-requiredRoles")]
    public List<RequiredRole> RequiredRoles { get; set; }
    
    [JsonPropertyName("genai-authenticationNotes")]
    public string AuthenticationNotes { get; set; }
    
    [JsonPropertyName("genai-commonIssues")]
    public List<CommonIssue> CommonIssues { get; set; }
    
    [JsonPropertyName("genai-bestPractices")]
    public List<BestPractice> BestPractices { get; set; }
    
    [JsonPropertyName("genai-serviceDocLink")]
    public string ServiceDocLink { get; set; }
    
    [JsonPropertyName("genai-additionalLinks")]
    public List<AdditionalLink> AdditionalLinks { get; set; }
}

public class MergedTool
{
    public string Command { get; set; }              // From static data
    public string MoreInfoLink { get; set; }         // From static data
    
    [JsonPropertyName("genai-shortDescription")]
    public string ShortDescription { get; set; }     // From AI
}
```

### Merging Logic

```csharp
// File: CSharpGenerator/Generators/HorizontalArticleGenerator.cs (continued)
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
```

### Template Rendering and File Generation

```csharp
// File: CSharpGenerator/Generators/HorizontalArticleGenerator.cs (continued)
private async Task RenderAndSaveArticle(HorizontalArticleTemplateData templateData)
{
    var templatePath = Path.Combine(_config.BasePath, "templates", 
        "horizontal-article-template.hbs");
    
    var templateEngine = new HandlebarsTemplateEngine();
    var renderedContent = templateEngine.Render(templatePath, templateData);
    
    // Generate output filename
    var outputDir = Path.Combine(_config.OutputBasePath, "horizontal-articles");
    Directory.CreateDirectory(outputDir);
    
    var filename = $"horizontal-article-{templateData.ServiceIdentifier}.md";
    var outputPath = Path.Combine(outputDir, filename);
    
    await File.WriteAllTextAsync(outputPath, renderedContent);
    
    Console.WriteLine($"Generated: {filename}");
}
```

## Complete Generation Flow

```csharp
// File: CSharpGenerator/Generators/HorizontalArticleGenerator.cs
public class HorizontalArticleGenerator
{
    private readonly Config _config;
    private readonly HandlebarsTemplateEngine _templateEngine;
    private readonly GenerativeAIClient _aiClient;
    
    public HorizontalArticleGenerator(Config config)
    {
        _config = config;
        _templateEngine = new HandlebarsTemplateEngine();
        _aiClient = new GenerativeAIClient(new GenerativeAIOptions
        {
            ApiKey = Environment.GetEnvironmentVariable("FOUNDRY_API_KEY"),
            Endpoint = Environment.GetEnvironmentVariable("FOUNDRY_ENDPOINT"),
            ModelName = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_NAME"),
            ApiVersion = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_API_VERSION")
        });
    }
    
    public async Task GenerateAllArticles()
    {
        Console.WriteLine("Phase 1: Extracting static data from generated content...");
        var staticDataList = ExtractStaticData();
        Console.WriteLine($"Found {staticDataList.Count} services");
        
        Console.WriteLine("\nPhase 2: Generating AI content for each service...");
        var articlesGenerated = 0;
        
        foreach (var staticData in staticDataList)
        {
            try
            {
                Console.WriteLine($"\n[{articlesGenerated + 1}/{staticDataList.Count}] Processing {staticData.ServiceBrandName}...");
                
                // Generate AI content
                var aiResponse = await GenerateAIContent(staticData);
                var aiData = ParseAIResponse(aiResponse);
                
                // Merge static + AI data
                var templateData = MergeData(staticData, aiData);
                
                // Render and save
                await RenderAndSaveArticle(templateData);
                
                articlesGenerated++;
                
                // Rate limiting (if needed)
                await Task.Delay(1000);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating article for {staticData.ServiceBrandName}: {ex.Message}");
                // Continue with next service
            }
        }
        
        Console.WriteLine($"\nPhase 3 Complete: Generated {articlesGenerated} horizontal articles");
    }
}
```

## Integration with Existing System - MINIMAL CHANGES APPROACH

### Key Principle: Reuse Existing Infrastructure

**NO CHANGES TO:**
- ❌ `Program.cs` - Keep existing entry points
- ❌ `Generate-MultiPageDocs.ps1` - Keep existing orchestration
- ❌ `DocumentationGenerator.cs` - Keep existing generator

**CREATE ONLY:**
- ✅ New standalone generator: `HorizontalArticleGenerator.cs`
- ✅ New standalone script: `Generate-HorizontalArticles.ps1`
- ✅ New models in `Models/` directory

### Entry Point - NEW STANDALONE PROGRAM

```csharp
// File: CSharpGenerator/HorizontalArticleProgram.cs (NEW FILE)
// Separate entry point - does NOT modify existing Program.cs
using System.Text.Json;
using CSharpGenerator.Generators;
using Shared;

namespace CSharpGenerator;

internal class HorizontalArticleProgram
{
    private static async Task<int> Main(string[] args)
    {
        // Load config using existing infrastructure
        var configPath = Path.Combine(AppContext.BaseDirectory, "../../../../config.json");
        Console.WriteLine($"Loading config from: {configPath}");
        var success = Config.Load(configPath);
        if (!success)
        {
            Console.Error.WriteLine("Failed to load configuration.");
            return 1;
        }

        // Validate environment variables
        var requiredVars = new[] { 
            "FOUNDRY_API_KEY", 
            "FOUNDRY_ENDPOINT", 
            "FOUNDRY_MODEL_NAME", 
            "FOUNDRY_MODEL_API_VERSION" 
        };
        
        foreach (var varName in requiredVars)
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(varName)))
            {
                Console.Error.WriteLine($"Error: Environment variable {varName} not set");
                return 1;
            }
        }

        // Run generator
        var generator = new HorizontalArticleGenerator();
        await generator.GenerateAllArticles();
        
        return 0;
    }
}
```

### PowerShell Orchestration - STANDALONE SCRIPT

```powershell
# File: docs-generation/Generate-HorizontalArticles.ps1 (NEW FILE)
# Does NOT modify Generate-MultiPageDocs.ps1
param(
    [switch]$SkipAIGeneration = $false
)

Write-Host "Horizontal Article Generation for Azure MCP Services" -ForegroundColor Cyan
Write-Host ""

# Ensure CLI output exists
$cliOutputPath = "generated/cli/cli-output.json"
if (-not (Test-Path $cliOutputPath)) {
    Write-Host "Error: CLI output not found at: $cliOutputPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "Please run one of the following first:" -ForegroundColor Yellow
    Write-Host "  ./run-mcp-cli-output.sh" -ForegroundColor White
    Write-Host "  pwsh ./docs-generation/Get-McpCliOutput.ps1" -ForegroundColor White
    exit 1
}

# Check environment variables
$requiredVars = @(
    "FOUNDRY_API_KEY",
    "FOUNDRY_ENDPOINT", 
    "FOUNDRY_MODEL_NAME",
    "FOUNDRY_MODEL_API_VERSION"
)

$missingVars = @()
foreach ($var in $requiredVars) {
    if (-not $env:$var) {
        $missingVars += $var
    }
}

if ($missingVars.Count -gt 0) {
    Write-Host "Error: Missing required environment variables:" -ForegroundColor Red
    foreach ($var in $missingVars) {
        Write-Host "  - $var" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "Set these variables before running this script." -ForegroundColor Yellow
    exit 1
}

# Build the horizontal article generator
Write-Host "Building horizontal article generator..." -ForegroundColor Cyan
Push-Location "CSharpGenerator"

# Build HorizontalArticleGenerator project (new csproj)
dotnet build HorizontalArticleGenerator.csproj --configuration Release --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Pop-Location
    Write-Host "Failed to build horizontal article generator" -ForegroundColor Red
    exit 1
}

# Run the generator
Write-Host "Generating horizontal articles with AI content..." -ForegroundColor Cyan
dotnet run --project HorizontalArticleGenerator.csproj --configuration Release --no-build

$exitCode = $LASTEXITCODE
Pop-Location

if ($exitCode -eq 0) {
    Write-Host ""
    Write-Host "✓ Horizontal articles generated successfully!" -ForegroundColor Green
    Write-Host "Output directory: generated/horizontal-articles/" -ForegroundColor Cyan
    
    # Show generated files
    if (Test-Path "generated/horizontal-articles") {
        $files = Get-ChildItem "generated/horizontal-articles" -Filter "*.md"
        Write-Host "Generated $($files.Count) articles" -ForegroundColor Green
    }
} else {
    Write-Host "Failed to generate horizontal articles (exit code: $exitCode)" -ForegroundColor Red
    exit $exitCode
}
```

### Alternative: Use Existing DocumentationGenerator (EVEN LESS CHANGES)

If we want to integrate into existing system without new entry point:

```csharp
// File: CSharpGenerator/DocumentationGenerator.cs
// Add ONE new method at the end of existing file

public static async Task<int> GenerateHorizontalArticlesAsync(
    string cliOutputFile,
    string outputDir)
{
    Console.WriteLine("Generating horizontal articles with AI content...");
    
    var generator = new HorizontalArticleGenerator();
    await generator.GenerateAllArticles();
    
    return 0;
}
```

Then add to Program.cs (minimal change):

```csharp
// In Program.cs, add to switch statement in Main()
case "generate-horizontal-articles":
    return await GenerateHorizontalArticles(args[1..]);

// Add new method
private static async Task<int> GenerateHorizontalArticles(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: CSharpGenerator generate-horizontal-articles <cli-output-json> <output-dir>");
        return 1;
    }
    
    return await DocumentationGenerator.GenerateHorizontalArticlesAsync(args[0], args[1]);
}
```

### Recommendation: **Standalone Approach**

Use the standalone `HorizontalArticleProgram.cs` + separate `.csproj` approach because:

1. **Zero modifications** to existing working code
2. **Independent development** - can iterate without breaking existing docs
3. **Separate build** - doesn't slow down main generator
4. **Easy testing** - can test in isolation
5. **Clear separation** - horizontal articles are conceptually different from parameter docs

## File Structure - MINIMAL CHANGES APPROACH

### Existing Files (NO MODIFICATIONS)
```
docs-generation/
├── CSharpGenerator/
│   ├── Program.cs                         # ❌ NO CHANGES
│   ├── DocumentationGenerator.cs          # ❌ NO CHANGES
│   ├── CSharpGenerator.csproj            # ❌ NO CHANGES
│   └── Generators/                        # ❌ NO CHANGES
│       ├── PageGenerator.cs
│       ├── ParameterGenerator.cs
│       └── ...
├── Generate-MultiPageDocs.ps1             # ❌ NO CHANGES
└── ...
```

### New Files (ADDITIONS ONLY)
```
docs-generation/
├── CSharpGenerator/
│   ├── HorizontalArticleGenerator.csproj  # ✅ NEW - Separate project
│   ├── HorizontalArticleProgram.cs        # ✅ NEW - Standalone entry point
│   ├── Generators/
│   │   └── HorizontalArticleGenerator.cs  # ✅ NEW - Main generator logic
│   └── Models/
│       ├── StaticArticleData.cs           # ✅ NEW - Phase 1 data
│       ├── AIGeneratedArticleData.cs      # ✅ NEW - Phase 2 data
│       └── HorizontalArticleTemplateData.cs  # ✅ NEW - Phase 3 merged data
├── prompts/
│   ├── horizontal-article-system-prompt.txt  # ✅ EXISTING (already updated)
│   └── horizontal-article-user-prompt.txt    # ✅ EXISTING (already updated)
├── templates/
│   └── horizontal-article-template.hbs       # ✅ EXISTING (already created)
├── Generate-HorizontalArticles.ps1           # ✅ NEW - Standalone script
└── generated/
    └── horizontal-articles/                  # ✅ NEW - Output directory
        ├── horizontal-article-storage.md
        ├── horizontal-article-aks.md
        └── ...
```

### Project File Structure

**HorizontalArticleGenerator.csproj** (NEW)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>CSharpGenerator</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <!-- Reference existing shared projects -->
    <ProjectReference Include="..\Shared\Shared.csproj" />
    <ProjectReference Include="..\GenerativeAI\GenerativeAI.csproj" />
    <ProjectReference Include="..\NaturalLanguageGenerator\NaturalLanguageGenerator.csproj" />
  </ItemGroup>

  <ItemGroup>
    <!-- Inherit Central Package Management versions -->
    <PackageReference Include="Handlebars.Net" />
  </ItemGroup>

  <!-- Link existing files to avoid duplication -->
  <ItemGroup>
    <Compile Include="HandlebarsTemplateEngine.cs" />
    <Compile Include="Config.cs" />
    <Compile Include="Models\*.cs" />
  </ItemGroup>
</Project>
```

### Benefits of This Approach

1. **Zero Risk**: Existing documentation generation continues unchanged
2. **Parallel Development**: Can develop/test horizontal articles independently  
3. **Separate Deployment**: Can run horizontal generation on different schedule
4. **Easy Rollback**: Delete new files if needed, no code to revert
5. **Clean Separation**: Horizontal articles are different output type
6. **Reuses Infrastructure**: Config, Handlebars, GenerativeAI client all shared

## Testing Strategy

### Unit Tests

```csharp
// File: CSharpGenerator.Tests/HorizontalArticleGeneratorTests.cs
[Fact]
public void ExtractStaticData_GroupsToolsByService()
{
    // Test tool grouping logic
}

[Fact]
public void ParseAIResponse_HandlesJsonCodeBlocks()
{
    // Test JSON extraction from markdown
}

[Fact]
public void MergeData_CombinesStaticAndAIFields()
{
    // Test merging logic
}
```

### Integration Tests

1. **Mock AI Response**: Test with pre-generated JSON responses
2. **Single Service**: Generate article for one service (e.g., Azure Storage)
3. **Verify Output**: Check markdown structure, links, required sections

## Error Handling

### Scenarios to Handle

1. **Missing Environment Variables**: Fail fast with clear message
2. **AI API Failure**: Log error, continue with next service
3. **Invalid JSON Response**: Log raw response, skip service
4. **Missing Static Data**: Log warning, use defaults
5. **Template Rendering Error**: Log error with service name

### Logging

```csharp
private async Task<AIGeneratedArticleData> GenerateWithRetry(
    StaticArticleData staticData, 
    int maxRetries = 3)
{
    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            var aiResponse = await GenerateAIContent(staticData);
            return ParseAIResponse(aiResponse);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Attempt {attempt}/{maxRetries} failed: {ex.Message}");
            if (attempt == maxRetries)
                throw;
            await Task.Delay(2000 * attempt); // Exponential backoff
        }
    }
    throw new InvalidOperationException("Should not reach here");
}
```

## Performance Considerations

1. **Batch Processing**: Process multiple services sequentially (parallel would exceed rate limits)
2. **Rate Limiting**: Add 1-2 second delay between AI calls
3. **Caching**: Cache AI responses to disk for re-runs
4. **Resume Support**: Save progress, allow resuming from last successful service

## Cost Estimation

- **Services**: ~44 service areas
- **AI Calls**: 1 per service = 44 calls
- **Tokens per call**: ~2000 input + ~3000 output = 5000 tokens
- **Total tokens**: 44 × 5000 = 220,000 tokens
- **Estimated cost**: $0.50 - $2.00 (depending on model pricing)
- **Time**: ~1-2 minutes with rate limiting

## Future Enhancements

1. **Incremental Updates**: Only regenerate articles for changed services
2. **Quality Validation**: Check AI output for required fields before rendering
3. **Human Review**: Generate draft articles for manual review/approval
4. **A/B Testing**: Generate multiple versions, select best
5. **Feedback Loop**: Track article usage, improve prompts based on metrics
