using System.CommandLine;
using Microsoft.Extensions.Logging;
using SkillsGen.Core.Assessment;
using SkillsGen.Core.Fetchers;
using SkillsGen.Core.Generation;
using SkillsGen.Core.Logging;
using SkillsGen.Core.Models;
using SkillsGen.Core.Orchestration;
using SkillsGen.Core.Parsers;
using SkillsGen.Core.PostProcessing;
using SkillsGen.Core.Validation;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});

var skillNameArg = new Argument<string>("skill-name", "The skill to generate (e.g., azure-storage)");
var allOption = new Option<bool>("--all", "Generate all skills from inventory");
var noLlmOption = new Option<bool>("--no-llm", "Disable LLM rewriting");
var dryRunOption = new Option<bool>("--dry-run", "Parse and validate only");
var sourceOption = new Option<string>("--source", () => "local", "Source: github or local");
var outOption = new Option<string>("--out", () => "../generated-skills/", "Output directory");
var forceOption = new Option<bool>("--force", "Write even if validation fails");
var verboseOption = new Option<bool>("--verbose", "Verbose output");
var sourcePathOption = new Option<string>("--source-path", () => "./skills-source/", "Path to local skills source directory");
var testsPathOption = new Option<string?>("--tests-path", "Path to tests directory (for triggers.test.ts). If not set, looks in source-path.");
var dataPathOption = new Option<string>("--data-path", () => "./data/", "Path to data directory");
var templatePathOption = new Option<string>("--template-path", () => "./templates/skill-page-template.hbs", "Path to Handlebars template");

var rootCommand = new RootCommand("Azure Skills Documentation Generator");

var generateSkillCommand = new Command("generate-skill", "Generate documentation for a single Azure skill")
{
    skillNameArg, noLlmOption, dryRunOption, sourceOption, outOption, forceOption, verboseOption,
    sourcePathOption, testsPathOption, dataPathOption, templatePathOption
};

var generateSkillsCommand = new Command("generate-skills", "Generate documentation for all Azure skills")
{
    allOption, noLlmOption, dryRunOption, sourceOption, outOption, forceOption, verboseOption,
    sourcePathOption, testsPathOption, dataPathOption, templatePathOption
};

generateSkillCommand.SetHandler(async (context) =>
{
    var skillName = context.ParseResult.GetValueForArgument(skillNameArg);
    var noLlm = context.ParseResult.GetValueForOption(noLlmOption);
    var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
    var source = context.ParseResult.GetValueForOption(sourceOption) ?? "local";
    var outputDir = context.ParseResult.GetValueForOption(outOption) ?? "./generated-skills/";
    var force = context.ParseResult.GetValueForOption(forceOption);
    var sourcePath = context.ParseResult.GetValueForOption(sourcePathOption) ?? "./skills-source/";
    var testsPath = context.ParseResult.GetValueForOption(testsPathOption);
    var dataPath = context.ParseResult.GetValueForOption(dataPathOption) ?? "./data/";
    var templatePath = context.ParseResult.GetValueForOption(templatePathOption) ?? "./templates/skill-page-template.hbs";

    var orchestrator = BuildOrchestrator(loggerFactory, source, sourcePath, testsPath, dataPath, templatePath, outputDir, noLlm, dryRun, force);
    var result = await orchestrator.ProcessSkillAsync(skillName);

    var status = result.Validation.IsValid ? "✅ PASSED" : "❌ FAILED";
    Console.WriteLine($"\n{status}: {skillName} (Tier {result.Tier}, {result.DurationMs}ms)");

    if (!result.Validation.IsValid)
    {
        foreach (var error in result.Validation.Errors)
            Console.WriteLine($"  ERROR: {error}");
    }
    foreach (var warning in result.Validation.Warnings)
        Console.WriteLine($"  WARN: {warning}");

    context.ExitCode = result.Validation.IsValid ? 0 : 1;
});

generateSkillsCommand.SetHandler(async (context) =>
{
    var noLlm = context.ParseResult.GetValueForOption(noLlmOption);
    var dryRun = context.ParseResult.GetValueForOption(dryRunOption);
    var source = context.ParseResult.GetValueForOption(sourceOption) ?? "local";
    var outputDir = context.ParseResult.GetValueForOption(outOption) ?? "./generated-skills/";
    var force = context.ParseResult.GetValueForOption(forceOption);
    var sourcePath = context.ParseResult.GetValueForOption(sourcePathOption) ?? "./skills-source/";
    var testsPath = context.ParseResult.GetValueForOption(testsPathOption);
    var dataPath = context.ParseResult.GetValueForOption(dataPathOption) ?? "./data/";
    var templatePath = context.ParseResult.GetValueForOption(templatePathOption) ?? "./templates/skill-page-template.hbs";

    var inventoryPath = Path.Combine(dataPath, "skills-inventory.json");
    var inventoryLoader = new SkillInventoryLoader(loggerFactory.CreateLogger<SkillInventoryLoader>());
    var skills = inventoryLoader.Load(inventoryPath);

    if (skills.Count == 0)
    {
        Console.WriteLine("[skills-gen] No skills found in inventory.");
        context.ExitCode = 1;
        return;
    }

    Console.WriteLine($"[skills-gen] Found {skills.Count} skills in inventory.");

    var orchestrator = BuildOrchestrator(loggerFactory, source, sourcePath, testsPath, dataPath, templatePath, outputDir, noLlm, dryRun, force);
    var report = await orchestrator.ProcessBatchAsync(skills);

    var succeeded = report.Results.Count(r => r.Validation.IsValid);
    var failed = report.Results.Count - succeeded;
    Console.WriteLine($"\n[skills-gen] Complete: {succeeded}/{report.Results.Count} passed, {failed} failed ({report.TotalDurationMs}ms)");

    context.ExitCode = failed > 0 ? 1 : 0;
});

rootCommand.AddCommand(generateSkillCommand);
rootCommand.AddCommand(generateSkillsCommand);

return await rootCommand.InvokeAsync(args);

static SkillPipelineOrchestrator BuildOrchestrator(
    ILoggerFactory loggerFactory,
    string source, string sourcePath, string? testsPath, string dataPath,
    string templatePath, string outputDir,
    bool noLlm, bool dryRun, bool force)
{
    var startupLogger = loggerFactory.CreateLogger("Startup");

    // --- Startup file validation ---
    // REQUIRED files — fail fast if missing
    var inventoryPath = Path.Combine(dataPath, "skills-inventory.json");
    if (!File.Exists(inventoryPath))
    {
        Console.Error.WriteLine($"❌ FATAL: Required file missing: {inventoryPath}");
        Environment.Exit(1);
    }
    if (!File.Exists(templatePath))
    {
        Console.Error.WriteLine($"❌ FATAL: Required file missing: {templatePath}");
        Environment.Exit(1);
    }

    // OPTIONAL files — warn if missing
    var replacementsPath = Path.Combine(dataPath, "static-text-replacement.json");
    var acronymsPath = Path.Combine(dataPath, "acronym-definitions.json");
    if (!File.Exists(replacementsPath))
        startupLogger.LogWarning("⚠️ Missing {Filename} — Acrolinx compliance will be degraded", "static-text-replacement.json");
    if (!File.Exists(acronymsPath))
        startupLogger.LogWarning("⚠️ Missing {Filename} — Acrolinx compliance will be degraded", "acronym-definitions.json");

    var systemPromptPath = Path.Combine(Path.GetDirectoryName(templatePath) ?? ".", "..", "prompts", "skill-page-system-prompt.txt");
    var userPromptPath = Path.Combine(Path.GetDirectoryName(templatePath) ?? ".", "..", "prompts", "skill-page-user-prompt-intro.txt");
    if (!File.Exists(systemPromptPath))
        startupLogger.LogWarning("⚠️ Missing prompt file: {Path}", systemPromptPath);
    if (!File.Exists(userPromptPath))
        startupLogger.LogWarning("⚠️ Missing prompt file: {Path}", userPromptPath);

    // --- Build components ---
    // Fetcher
    ISkillSourceFetcher fetcher = source == "github"
        ? new GitHubSkillFetcher(new HttpClient(), loggerFactory.CreateLogger<GitHubSkillFetcher>())
        : new LocalSkillFetcher(sourcePath, loggerFactory.CreateLogger<LocalSkillFetcher>(), testsPath);

    // Parsers
    var parser = new SkillMarkdownParser();
    var triggerParser = new TriggerTestParser();

    // Assessment
    var tierAssessor = new TierAssessor();

    // LLM Rewriter
    ILlmRewriter rewriter;
    if (noLlm)
    {
        rewriter = new NoOpRewriter();
    }
    else
    {
        var apiKey = Environment.GetEnvironmentVariable("FOUNDRY_API_KEY");
        var endpoint = Environment.GetEnvironmentVariable("FOUNDRY_ENDPOINT");
        var modelName = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_NAME") ?? "gpt-4o";

        if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(endpoint))
        {
            var acrolinxPath = Path.Combine(dataPath, "shared-acrolinx-rules.txt");

            var systemPrompt = File.Exists(systemPromptPath) ? File.ReadAllText(systemPromptPath) : "You are a technical writer.";
            var userPrompt = File.Exists(userPromptPath) ? File.ReadAllText(userPromptPath) : "Write about {{skillName}}: {{description}}";
            var acrolinxRules = File.Exists(acrolinxPath) ? File.ReadAllText(acrolinxPath) : null;

            rewriter = new AzureOpenAiRewriter(endpoint, apiKey, modelName,
                systemPrompt, userPrompt, acrolinxRules,
                loggerFactory.CreateLogger<AzureOpenAiRewriter>());
        }
        else
        {
            Console.WriteLine("[skills-gen] No AI credentials found, using no-op rewriter.");
            rewriter = new NoOpRewriter();
        }
    }

    // Template
    var templateContent = File.Exists(templatePath)
        ? File.ReadAllText(templatePath)
        : throw new FileNotFoundException($"Template not found: {templatePath}");
    var pageGenerator = new SkillPageGenerator(templateContent, loggerFactory.CreateLogger<SkillPageGenerator>());

    // Post-processor
    var replacementsJson = File.Exists(replacementsPath) ? File.ReadAllText(replacementsPath) : null;
    var acronymsJson = File.Exists(acronymsPath) ? File.ReadAllText(acronymsPath) : null;
    var postProcessor = new AcrolinxPostProcessor(replacementsJson, acronymsJson,
        loggerFactory.CreateLogger<AcrolinxPostProcessor>());

    // Validator
    var validator = new SkillPageValidator();

    // Logger
    var skillsLogger = new SkillsLogger(loggerFactory.CreateLogger<SkillsLogger>(), Path.Combine(outputDir, "logs"));

    return new SkillPipelineOrchestrator(
        fetcher, parser, triggerParser, tierAssessor,
        rewriter, pageGenerator, postProcessor, validator,
        skillsLogger, outputDir, dryRun, force);
}
