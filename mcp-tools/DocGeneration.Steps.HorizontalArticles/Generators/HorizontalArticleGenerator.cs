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
using Microsoft.Extensions.AI;
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
    /// Per-tool calls use the shared client's 8000-token default to accommodate reasoning tokens.
    /// Namespace summary calls use a compact tool list (command + description only), so a lower cap applies.
    /// </summary>
    internal static int CalculateMaxTokens(int toolCount, bool isPerToolCall)
    {
        if (isPerToolCall) return 8000;
        // Namespace summary: compact list only; lower base and per-tool factor than full article
        var calculatedTokens = 2000 + (toolCount * 150);
        return Math.Clamp(calculatedTokens, 3000, 8000);
    }

    /// <summary>
    /// Identifies one of the four small, focused namespace-fragment AI calls that replaced the
    /// single broad namespace-summary call (which was truncated on gpt-5-mini and then blindly
    /// retried 3× by <see cref="WithRetry{T}"/>). Each fragment has its own compact prompt files
    /// and output token budget — see <see cref="CalculateMaxTokens(NamespaceFragment)"/>.
    /// </summary>
    internal enum NamespaceFragment
    {
        /// <summary>ServiceShortDescription + ServiceOverview.</summary>
        Overview,
        /// <summary>ServiceSpecificPrerequisites + RequiredRoles.</summary>
        Access,
        /// <summary>BestPractices.</summary>
        BestPractices,
        /// <summary>ServiceDocLink + AdditionalLinks.</summary>
        Links
    }

    /// <summary>
    /// Output token budget for each small namespace-fragment call. Each fragment returns 1-2 short
    /// JSON fields/arrays, never the full seven-field namespace payload that used to overflow the
    /// output budget on reasoning models.
    /// </summary>
    internal static int CalculateMaxTokens(NamespaceFragment fragment) => fragment switch
    {
        NamespaceFragment.Overview => 1500,
        NamespaceFragment.Access => 1500,
        NamespaceFragment.BestPractices => 1500,
        NamespaceFragment.Links => 1500,
        _ => throw new ArgumentOutOfRangeException(nameof(fragment), fragment, "Unknown namespace fragment.")
    };

    // Extracted method for generating a single article
    private async Task<bool> GenerateSingleArticleAsync(StaticArticleData staticData, string outputDir, string progress)
    {
        try
        {
            Console.WriteLine($"{progress} Processing {staticData.ServiceBrandName}...");

            if (!AreCorePromptFilesPresent())
            {
                Console.WriteLine($"{progress} ✗ Required per-tool or namespace-fragment prompt files are missing; obsolete monolithic generation is disabled.");
                return false;
            }

            var (aiData, failureReason) = await GeneratePerToolAiDataAsync(staticData, progress);
            if (aiData is null)
            {
                Console.WriteLine($"{progress} ✗ {failureReason}");
                return false;
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

    // Prompt and template file names (relative to project subdir)
    private const string PROJECT_SUBDIR = "DocGeneration.Steps.HorizontalArticles";
    private const string SYSTEM_PROMPT_FILE = "horizontal-article-system-prompt.txt";
    private const string USER_PROMPT_FILE = "horizontal-article-user-prompt.txt";
    private const string TEMPLATE_FILE = "horizontal-article-template.hbs";
    private const string TOOL_SYSTEM_PROMPT_FILE = "horizontal-article-tool-system-prompt.txt";
    private const string TOOL_USER_PROMPT_FILE = "horizontal-article-tool-user-prompt.txt";
    private const string NAMESPACE_USER_PROMPT_FILE = "horizontal-article-namespace-user-prompt.txt";

    // Step 6 surgical refactor: the single broad namespace-summary call (33 KB legacy system
    // prompt + namespace-user-prompt, seven fields) was truncated on gpt-5-mini and then retried
    // 3× for a guaranteed-identical failure. It is replaced by four small, focused fragment calls,
    // each with its own compact, service-agnostic prompt pair — never the legacy SYSTEM_PROMPT_FILE.
    private const string NAMESPACE_OVERVIEW_SYSTEM_PROMPT_FILE = "horizontal-article-namespace-overview-system-prompt.txt";
    private const string NAMESPACE_OVERVIEW_USER_PROMPT_FILE = "horizontal-article-namespace-overview-user-prompt.txt";
    private const string NAMESPACE_ACCESS_SYSTEM_PROMPT_FILE = "horizontal-article-namespace-access-system-prompt.txt";
    private const string NAMESPACE_ACCESS_USER_PROMPT_FILE = "horizontal-article-namespace-access-user-prompt.txt";
    private const string NAMESPACE_BEST_PRACTICES_SYSTEM_PROMPT_FILE = "horizontal-article-namespace-best-practices-system-prompt.txt";
    private const string NAMESPACE_BEST_PRACTICES_USER_PROMPT_FILE = "horizontal-article-namespace-best-practices-user-prompt.txt";
    private const string NAMESPACE_LINKS_SYSTEM_PROMPT_FILE = "horizontal-article-namespace-links-system-prompt.txt";
    private const string NAMESPACE_LINKS_USER_PROMPT_FILE = "horizontal-article-namespace-links-user-prompt.txt";

    private readonly string _cliOutputPath;
    private readonly string _outputDir;
    private readonly string _promptOutputDir;
    private readonly string _outputBasePath;

    // When set, all prompt/template paths are resolved relative to this root (mcp-tools/).
    // When null, falls back to CWD-relative resolution (correct when running as subprocess
    // where the working directory is already set to mcp-tools/).
    private readonly string? _mcpToolsRoot;

    private readonly GenerativeAIClient _aiClient;
    private readonly bool _useTextTransformation;
    private readonly bool _generateAllArticles;
    private readonly TransformationEngine? _transformationEngine;
    private readonly int _aiMaxAttempts;
    private readonly Func<int, TimeSpan> _aiRetryDelay;

    /// <summary>Default exponential backoff for AI retries: 1s, 2s, 4s, … (#661).</summary>
    private static TimeSpan DefaultAiRetryDelay(int attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1));

    /// <summary>
    /// Retries <paramref name="operation"/> up to <paramref name="maxAttempts"/> times, awaiting
    /// <paramref name="delay"/> between attempts. The final attempt is not caught, so its exception
    /// propagates to the caller (which then applies its static fallback). Fixes #661.
    /// <paramref name="shouldRetry"/> optionally classifies whether a given failure is worth
    /// retrying at all — when it returns <c>false</c> the exception propagates immediately (no
    /// delay, no further attempts), instead of being retried up to <paramref name="maxAttempts"/>
    /// times. Defaults to "retry everything" when omitted, preserving pre-existing behavior for
    /// any caller that does not pass a classifier.
    /// </summary>
    internal static async Task<T> WithRetry<T>(
        Func<Task<T>> operation,
        int maxAttempts,
        Func<int, TimeSpan> delay,
        Func<Exception, bool>? shouldRetry = null)
    {
        shouldRetry ??= static _ => true;

        for (int attempt = 1; attempt < maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (shouldRetry(ex))
            {
                await Task.Delay(delay(attempt));
            }
        }

        return await operation(); // final attempt — let it throw
    }

    /// <summary>
    /// Invokes the AI chat completion with transient-failure retry/backoff (#661). Callers wrap this
    /// in their own try/catch so that, once all retries are exhausted, they fall back to static content.
    /// <paramref name="operation"/> and <paramref name="toolOrNamespace"/> are forwarded to
    /// <see cref="GenerativeAIClient.GetChatCompletionAsync"/> for status-log and tracing context —
    /// every call site must pass meaningful values so log lines never show <c>target=unknown</c>.
    /// Only genuinely transient failures are retried; see <see cref="IsRetryableAiFailure"/>.
    /// </summary>
    internal Task<string> CallAiWithRetryAsync(
        string systemPrompt,
        string userPrompt,
        int maxTokens,
        string? operation = null,
        string? toolOrNamespace = null)
        => WithRetry(
            () => _aiClient.GetChatCompletionAsync(
                systemPrompt, userPrompt, maxTokens,
                toolOrNamespace: toolOrNamespace,
                operation: operation,
                reasoningEffort: ReasoningEffort.Low,
                responseFormat: ChatResponseFormat.Json),
            _aiMaxAttempts,
            _aiRetryDelay,
            IsRetryableAiFailure);

    /// <summary>
    /// Positively identifies transient failures worth retrying: timeouts, network failures without
    /// an HTTP response, HTTP 429, and HTTP 5xx. All other errors are deterministic or require
    /// operator/configuration changes and are not retried.
    /// </summary>
    internal static bool IsRetryableAiFailure(Exception ex)
    {
        // Token truncation: GenerativeAIClient.GetChatCompletionAsync throws this specific
        // InvalidOperationException when FinishReason == Length. Retrying with the same maxTokens
        // budget produces the same truncation every time.
        if (ex is InvalidOperationException ioe &&
            ioe.Message.Contains("truncated due to token limit", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Malformed JSON / schema errors are deterministic parsing failures, not transient.
        if (ex is JsonException)
        {
            return false;
        }

        var statusCode = GenerativeAIClient.TryGetHttpStatusCode(ex);
        if (statusCode.HasValue)
        {
            return statusCode == 429 || statusCode >= 500;
        }

        return ex is TimeoutException ||
               ex is HttpRequestException;
    }
    /// <summary>
    /// Resolves a prompt file path.  When <c>mcpToolsRoot</c> was supplied to the constructor
    /// the path is anchored to that directory; otherwise it resolves from the current working
    /// directory (the correct behaviour when running as a dotnet subprocess with CWD = mcp-tools/).
    /// </summary>
    internal string GetPromptPath(string fileName) =>
        _mcpToolsRoot is not null
            ? Path.Combine(_mcpToolsRoot, PROJECT_SUBDIR, "prompts", fileName)
            : Path.GetFullPath(Path.Combine(".", PROJECT_SUBDIR, "prompts", fileName));

    /// <summary>
    /// Resolves a template file path using the same strategy as <see cref="GetPromptPath"/>.
    /// </summary>
    internal string GetTemplatePath(string fileName) =>
        _mcpToolsRoot is not null
            ? Path.Combine(_mcpToolsRoot, PROJECT_SUBDIR, "templates", fileName)
            : Path.GetFullPath(Path.Combine(".", PROJECT_SUBDIR, "templates", fileName));

    /// <summary>
    /// True when the per-tool prompt pair and all four namespace-fragment prompt pairs are present.
    /// Missing prompts fail generation before any AI request; the obsolete monolithic path is never used.
    /// </summary>
    private bool AreCorePromptFilesPresent() =>
        File.Exists(GetPromptPath(TOOL_SYSTEM_PROMPT_FILE)) &&
        File.Exists(GetPromptPath(TOOL_USER_PROMPT_FILE)) &&
        File.Exists(GetPromptPath(NAMESPACE_OVERVIEW_SYSTEM_PROMPT_FILE)) &&
        File.Exists(GetPromptPath(NAMESPACE_OVERVIEW_USER_PROMPT_FILE)) &&
        File.Exists(GetPromptPath(NAMESPACE_ACCESS_SYSTEM_PROMPT_FILE)) &&
        File.Exists(GetPromptPath(NAMESPACE_ACCESS_USER_PROMPT_FILE)) &&
        File.Exists(GetPromptPath(NAMESPACE_BEST_PRACTICES_SYSTEM_PROMPT_FILE)) &&
        File.Exists(GetPromptPath(NAMESPACE_BEST_PRACTICES_USER_PROMPT_FILE)) &&
        File.Exists(GetPromptPath(NAMESPACE_LINKS_SYSTEM_PROMPT_FILE)) &&
        File.Exists(GetPromptPath(NAMESPACE_LINKS_USER_PROMPT_FILE));

    public HorizontalArticleGenerator(GenerativeAIOptions options, bool useTextTransformation = false, bool generateAllArticles = false, TransformationEngine? transformationEngine = null, string? outputBasePath = null, string? mcpToolsRoot = null)
    {
        if (!options.UseDefaultCredential && string.IsNullOrEmpty(options.ApiKey)) throw new InvalidOperationException("FOUNDRY_API_KEY not set");
        if (string.IsNullOrEmpty(options.Endpoint)) throw new InvalidOperationException("FOUNDRY_ENDPOINT not set");
        if (string.IsNullOrEmpty(options.Deployment)) throw new InvalidOperationException("FOUNDRY_MODEL_NAME not set");
        if (string.IsNullOrEmpty(options.ApiVersion)) throw new InvalidOperationException("FOUNDRY_MODEL_API_VERSION not set");
        _aiClient = new GenerativeAIClient(options);
        _useTextTransformation = useTextTransformation;
        _generateAllArticles = generateAllArticles;
        _transformationEngine = transformationEngine;
        _mcpToolsRoot = string.IsNullOrWhiteSpace(mcpToolsRoot) ? null : Path.GetFullPath(mcpToolsRoot);
        _outputBasePath = outputBasePath != null ? Path.GetFullPath(outputBasePath) : DefaultOutputBase;
        _cliOutputPath = Path.Combine(_outputBasePath, "cli", "cli-output.json");
        _outputDir = Path.Combine(_outputBasePath, "horizontal-articles");
        _promptOutputDir = Path.Combine(_outputBasePath, "horizontal-article-prompts");
        _aiMaxAttempts = 3;
        _aiRetryDelay = DefaultAiRetryDelay;
    }

    /// <summary>
    /// Test-only constructor that injects a pre-built <see cref="GenerativeAIClient"/> and retry
    /// configuration, bypassing environment-variable validation. Used to exercise the #661 retry
    /// seam deterministically (a fake <c>IChatClient</c> + zero backoff delay).
    /// </summary>
    internal HorizontalArticleGenerator(
        GenerativeAIClient aiClient,
        string? outputBasePath,
        string? mcpToolsRoot,
        int aiMaxAttempts,
        Func<int, TimeSpan>? aiRetryDelay)
    {
        _aiClient = aiClient ?? throw new ArgumentNullException(nameof(aiClient));
        _useTextTransformation = false;
        _generateAllArticles = false;
        _transformationEngine = null;
        _mcpToolsRoot = string.IsNullOrWhiteSpace(mcpToolsRoot) ? null : Path.GetFullPath(mcpToolsRoot);
        _outputBasePath = outputBasePath != null ? Path.GetFullPath(outputBasePath) : DefaultOutputBase;
        _cliOutputPath = Path.Combine(_outputBasePath, "cli", "cli-output.json");
        _outputDir = Path.Combine(_outputBasePath, "horizontal-articles");
        _promptOutputDir = Path.Combine(_outputBasePath, "horizontal-article-prompts");
        _aiMaxAttempts = aiMaxAttempts;
        _aiRetryDelay = aiRetryDelay ?? DefaultAiRetryDelay;
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

        // Current per-tool + namespace-fragment AI generation path — the same one used by the
        // standalone console generator (GenerateSingleArticleAsync). Intentionally does NOT fall
        // back to the obsolete monolithic GenerateAIContent call: that method causes token
        // overflow on large namespaces and is no longer part of the supported pipeline path.
        if (!AreCorePromptFilesPresent())
        {
            throw new InvalidOperationException(
                "Horizontal article generation requires the per-tool AI prompt files and the four namespace-fragment " +
                $"AI prompt files ({TOOL_SYSTEM_PROMPT_FILE}, {TOOL_USER_PROMPT_FILE}, {NAMESPACE_OVERVIEW_USER_PROMPT_FILE}, " +
                $"{NAMESPACE_ACCESS_USER_PROMPT_FILE}, {NAMESPACE_BEST_PRACTICES_USER_PROMPT_FILE}, {NAMESPACE_LINKS_USER_PROMPT_FILE}), " +
                "not all of which " +
                "were found. The obsolete monolithic single-call AI generation path is not used by the pipeline " +
                "reducer and has no fallback here.");
        }

        var (aiData, failureReason) = await GeneratePerToolAiDataAsync(staticData, "[reducer]");
        if (aiData is null)
        {
            throw new InvalidOperationException(
                failureReason ?? "Per-tool + namespace-fragment AI generation failed for an unknown reason.");
        }

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
    /// Generates AI content for a single article using the current, supported path: one bounded
    /// per-tool AI call per tool (<see cref="GenerateAIContentForTool"/>), followed by a single
    /// namespace-summary result, aggregated via <see cref="AggregateAIData"/>.
    ///
    /// UPDATED (Step 6 surgical refactor): the single broad namespace-summary call is replaced by
    /// four small, focused fragment calls — <see cref="GenerateNamespaceOverviewAIContent"/>,
    /// <see cref="GenerateNamespaceAccessAIContent"/>, <see cref="GenerateNamespaceBestPracticesAIContent"/>,
    /// and <see cref="GenerateNamespaceLinksAIContent"/> — deterministically stitched back together
    /// by <see cref="StitchNamespaceSummary"/> before aggregation. Shared by both the console-mode generator
    /// (<see cref="GenerateSingleArticleAsync(StaticArticleData, string, string)"/>) and the
    /// PipelineRunner reducer path (<see cref="GenerateArticleMarkdownAsync"/>) so both use
    /// identical, service-agnostic AI generation logic. Each per-tool/summary call already catches
    /// its own AI failures internally (including the universal offline gate in
    /// <see cref="GenerativeAIClient"/>) and returns empty/static data rather than throwing; this
    /// method's only failure signal is an empty required overview-fragment field, which is
    /// reported back to the caller as a non-null <c>FailureReason</c> rather than thrown, so callers
    /// decide how to surface "AI-required work is incomplete" (never as a false success).
    /// </summary>
    private async Task<(AIGeneratedArticleData? Data, string? FailureReason)> GeneratePerToolAiDataAsync(
        StaticArticleData staticData,
        string progress)
    {
        // Per-tool AI calls: one per tool with enough output budget for reasoning models.
        Console.WriteLine($"{progress} Generating per-tool AI content ({staticData.Tools.Count} tools)...");
        var perToolResults = new List<PerToolAIData>();
        for (int i = 0; i < staticData.Tools.Count; i++)
        {
            var tool = staticData.Tools[i];
            Console.WriteLine($"{progress} ({i + 1}/{staticData.Tools.Count}) Tool: {tool.Command}");
            var perToolData = await GenerateAIContentForTool(tool, staticData.ServiceBrandName, staticData.ServiceIdentifier, i);
            perToolResults.Add(perToolData);
        }

        // Namespace-fragment AI calls: four small, focused calls after all per-tool calls complete,
        // replacing the single broad namespace-summary call that used to overflow its output budget
        // and then get retried 3× for a guaranteed-identical truncation failure.
        Console.WriteLine($"{progress} Generating namespace overview...");
        var overview = await GenerateNamespaceOverviewAIContent(staticData);
        Console.WriteLine($"{progress} Generating namespace access (prerequisites + RBAC)...");
        var access = await GenerateNamespaceAccessAIContent(staticData);
        Console.WriteLine($"{progress} Generating namespace best practices...");
        var bestPractices = await GenerateNamespaceBestPracticesAIContent(staticData);
        Console.WriteLine($"{progress} Generating namespace links...");
        var links = await GenerateNamespaceLinksAIContent(staticData);

        var summaryData = StitchNamespaceSummary(overview, access, bestPractices, links);
        var aggregated = AggregateAIData(staticData, perToolResults, summaryData);

        // Fail fast if namespace summary returned empty required fields (indicates failed AI call)
        if (string.IsNullOrWhiteSpace(aggregated.ServiceShortDescription) || string.IsNullOrWhiteSpace(aggregated.ServiceOverview))
        {
            return (null, $"Namespace overview fragment returned empty required fields for {staticData.ServiceBrandName} — article generation failed.");
        }

        return (aggregated, null);
    }

    /// <summary>
    /// Deterministically stitches the four small namespace-fragment results into a single
    /// <see cref="NamespaceSummaryAIData"/> — a pure, no-AI, no-I/O mapping so the rest of the
    /// pipeline (<see cref="AggregateAIData"/>, <c>ArticleContentProcessor</c>, template rendering)
    /// is unaffected by the fragment-call refactor.
    /// </summary>
    internal static NamespaceSummaryAIData StitchNamespaceSummary(
        NamespaceOverviewFragment overview,
        NamespaceAccessFragment access,
        NamespaceBestPracticesFragment bestPractices,
        NamespaceLinksFragment links)
    {
        return new NamespaceSummaryAIData
        {
            ServiceShortDescription = overview.ServiceShortDescription,
            ServiceOverview = overview.ServiceOverview,
            ServiceSpecificPrerequisites = access.ServiceSpecificPrerequisites,
            RequiredRoles = access.RequiredRoles,
            BestPractices = bestPractices.BestPractices,
            ServiceDocLink = links.ServiceDocLink,
            AdditionalLinks = links.AdditionalLinks
        };
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

        // Apply deterministic management-plane-first tool ordering (#660) so the
        // per-tool AI loop and AggregateAIData receive tools in a stable, readable
        // order regardless of the order they appear in the CLI metadata.
        foreach (var data in serviceDataList)
        {
            data.Tools = DeterministicHorizontalHelpers.OrderToolsByPlane(data.Tools);
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
        var systemPromptPath = GetPromptPath(SYSTEM_PROMPT_FILE);
        var userPromptPath = GetPromptPath(USER_PROMPT_FILE);
        
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

        // Call AI client (with transient-failure retry/backoff — #661)
        var response = await CallAiWithRetryAsync(
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
    internal async Task<PerToolAIData> GenerateAIContentForTool(
        HorizontalToolSummary tool,
        string serviceBrandName,
        string serviceIdentifier,
        int toolIndex)
    {
        var systemPromptPath = GetPromptPath(TOOL_SYSTEM_PROMPT_FILE);
        var userPromptPath   = GetPromptPath(TOOL_USER_PROMPT_FILE);

        if (!File.Exists(systemPromptPath) || !File.Exists(userPromptPath))
        {
            Console.WriteLine($"    ⚠️  Per-tool prompt files not found; using static description as fallback for: {tool.Command}");
            return new PerToolAIData { Command = tool.Command, ShortDescription = tool.Description };
        }

        string? promptFilePath = null;
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
            promptFilePath = Path.Combine(_promptOutputDir, promptFileName);
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
            var response = await CallAiWithRetryAsync(
                systemPrompt, userPrompt, maxTokens,
                operation: "per-tool", toolOrNamespace: tool.Command);

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
            await AppendAiFailureToPromptArtifactAsync(promptFilePath, ex);
            Console.WriteLine($"    ⚠️  AI call failed for tool {tool.Command}: {ex.Message} — using static fallback.");
            return new PerToolAIData { Command = tool.Command, ShortDescription = tool.Description };
        }
    }

    /// <summary>
    /// Calls AI once for the namespace "overview" fragment (service short description + overview
    /// paragraph). Required — see <see cref="GeneratePerToolAiDataAsync"/>, which treats an empty
    /// result from this fragment as fatal.
    /// </summary>
    internal Task<NamespaceOverviewFragment> GenerateNamespaceOverviewAIContent(StaticArticleData staticData) =>
        GenerateNamespaceFragmentAsync<NamespaceOverviewFragment>(
            staticData,
            NAMESPACE_OVERVIEW_SYSTEM_PROMPT_FILE,
            NAMESPACE_OVERVIEW_USER_PROMPT_FILE,
            fragmentSlug: "overview",
            fragmentLabel: "overview",
            fragmentBudget: NamespaceFragment.Overview,
            includeToolList: true);

    /// <summary>
    /// Calls AI once for the namespace "access" fragment (service-specific prerequisites + required
    /// RBAC roles), grounded to the compact tool list so role selection matches minimum privilege.
    /// </summary>
    internal Task<NamespaceAccessFragment> GenerateNamespaceAccessAIContent(StaticArticleData staticData) =>
        GenerateNamespaceFragmentAsync<NamespaceAccessFragment>(
            staticData,
            NAMESPACE_ACCESS_SYSTEM_PROMPT_FILE,
            NAMESPACE_ACCESS_USER_PROMPT_FILE,
            fragmentSlug: "access",
            fragmentLabel: "access",
            fragmentBudget: NamespaceFragment.Access,
            includeToolList: true);

    /// <summary>
    /// Calls AI once for the namespace "best practices" fragment, grounded to the compact tool list.
    /// </summary>
    internal Task<NamespaceBestPracticesFragment> GenerateNamespaceBestPracticesAIContent(StaticArticleData staticData) =>
        GenerateNamespaceFragmentAsync<NamespaceBestPracticesFragment>(
            staticData,
            NAMESPACE_BEST_PRACTICES_SYSTEM_PROMPT_FILE,
            NAMESPACE_BEST_PRACTICES_USER_PROMPT_FILE,
            fragmentSlug: "best-practices",
            fragmentLabel: "best practices",
            fragmentBudget: NamespaceFragment.BestPractices,
            includeToolList: true);

    /// <summary>
    /// Calls AI once for the namespace "links" fragment (primary service doc link + additional links),
    /// grounded to the tool list so broad namespace brands do not pull in unrelated documentation.
    /// </summary>
    internal Task<NamespaceLinksFragment> GenerateNamespaceLinksAIContent(StaticArticleData staticData) =>
        GenerateNamespaceFragmentAsync<NamespaceLinksFragment>(
            staticData,
            NAMESPACE_LINKS_SYSTEM_PROMPT_FILE,
            NAMESPACE_LINKS_USER_PROMPT_FILE,
            fragmentSlug: "links",
            fragmentLabel: "links",
            fragmentBudget: NamespaceFragment.Links,
            includeToolList: true);

    /// <summary>
    /// Shared implementation behind the four small namespace-fragment AI calls (overview, access,
    /// best practices, links) that replaced the single broad namespace-summary call. Each fragment
    /// uses its own compact, universal/service-agnostic prompt pair — never the 33 KB legacy
    /// <see cref="SYSTEM_PROMPT_FILE"/> — and a small output token budget (see
    /// <see cref="CalculateMaxTokens(NamespaceFragment)"/>). An empty token-truncated response gets
    /// one adaptive retry with a larger, capped budget; partial responses are not retried. Missing
    /// prompt files degrade gracefully to an empty fragment (mirrors
    /// <see cref="GenerateAIContentForTool"/>); the same is true for any AI call failure that
    /// survives retry, so exactly one fragment failing never blocks the other three from
    /// contributing their content — only the required overview fragment is fatal (see
    /// <see cref="GeneratePerToolAiDataAsync"/>).
    /// </summary>
    private async Task<TFragment> GenerateNamespaceFragmentAsync<TFragment>(
        StaticArticleData staticData,
        string systemPromptFile,
        string userPromptFile,
        string fragmentSlug,
        string fragmentLabel,
        NamespaceFragment fragmentBudget,
        bool includeToolList)
        where TFragment : new()
    {
        var systemPromptPath = GetPromptPath(systemPromptFile);
        var userPromptPath = GetPromptPath(userPromptFile);

        if (!File.Exists(systemPromptPath) || !File.Exists(userPromptPath))
        {
            Console.WriteLine($"    ⚠️  Namespace {fragmentLabel} prompt files not found; using empty {fragmentLabel} fragment for: {staticData.ServiceBrandName}");
            return new TFragment();
        }

        string? promptFilePath = null;
        try
        {
            var systemPrompt = await File.ReadAllTextAsync(systemPromptPath);
            systemPrompt = PromptTokenResolver.Resolve(systemPrompt, Path.Combine(AppContext.BaseDirectory, "data"));
            var userPromptTemplate = await File.ReadAllTextAsync(userPromptPath);

            var handlebars = HandlebarsDotNet.Handlebars.Create();
            var userPromptCompiled = handlebars.Compile(userPromptTemplate);

            // Tool-grounded fragments receive the compact command surface as their capability boundary.
            object promptContext = includeToolList
                ? new
                {
                    serviceBrandName = staticData.ServiceBrandName,
                    serviceIdentifier = staticData.ServiceIdentifier,
                    toolsReferenceLink = staticData.ToolsReferenceLink,
                    tools = staticData.Tools.Select(t => new { command = t.Command, description = t.Description })
                }
                : new
                {
                    serviceBrandName = staticData.ServiceBrandName,
                    serviceIdentifier = staticData.ServiceIdentifier,
                    toolsReferenceLink = staticData.ToolsReferenceLink
                };

            var userPrompt = userPromptCompiled(promptContext);

            // Save prompt for debugging — clear, component-specific filename per fragment.
            Directory.CreateDirectory(_promptOutputDir);
            var promptFileName = $"horizontal-article-{staticData.ServiceIdentifier}-namespace-{fragmentSlug}-prompt.md";
            promptFilePath = Path.Combine(_promptOutputDir, promptFileName);
            var promptContent = $"""
# Namespace {fragmentLabel} Prompt: {staticData.ServiceBrandName}

Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC

## System Prompt

{systemPrompt}

## User Prompt

{userPrompt}
""";
            await File.WriteAllTextAsync(promptFilePath, promptContent, Encoding.UTF8);

            var maxTokens = CalculateMaxTokens(fragmentBudget);
            string response;
            try
            {
                response = await CallAiWithRetryAsync(
                    systemPrompt, userPrompt, maxTokens,
                    operation: $"namespace-{fragmentSlug}", toolOrNamespace: staticData.ServiceIdentifier);
            }
            catch (AiResponseTruncatedException ex) when (string.IsNullOrWhiteSpace(ex.ResponseContent))
            {
                await AppendAiFailureToPromptArtifactAsync(promptFilePath, ex);

                const int adaptiveRetryCeiling = 3000;
                var adaptiveMaxTokens = Math.Min(maxTokens * 2, adaptiveRetryCeiling);
                Console.WriteLine(
                    $"    ↻ Namespace {fragmentLabel} response was empty after truncation; " +
                    $"adaptive budget escalation from {maxTokens} to {adaptiveMaxTokens} output tokens.");

                response = await CallAiWithRetryAsync(
                    systemPrompt, userPrompt, adaptiveMaxTokens,
                    operation: $"namespace-{fragmentSlug}", toolOrNamespace: staticData.ServiceIdentifier);
            }

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
            return JsonSerializer.Deserialize<TFragment>(jsonContent, options) ?? new TFragment();
        }
        catch (Exception ex)
        {
            await AppendAiFailureToPromptArtifactAsync(promptFilePath, ex);
            Console.WriteLine($"    ⚠️  Namespace {fragmentLabel} AI call failed for {staticData.ServiceBrandName}: {ex.Message} — using empty {fragmentLabel} fragment.");
            return new TFragment();
        }
    }

    private static Task AppendAiFailureToPromptArtifactAsync(string? promptFilePath, Exception exception)
    {
        if (string.IsNullOrWhiteSpace(promptFilePath) || !File.Exists(promptFilePath))
        {
            return Task.CompletedTask;
        }

        var responseSection = exception is AiResponseTruncatedException truncated
            ? $"""


## AI Response (truncated)

```json
{truncated.ResponseContent}
```
"""
            : """


## AI Response

_No response was received._
""";

        return File.AppendAllTextAsync(
            promptFilePath,
            $"""
{responseSection}

## AI Error

{exception.Message}
""",
            Encoding.UTF8);
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
        var templatePath = GetTemplatePath(TEMPLATE_FILE);
        
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
