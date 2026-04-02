using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SkillsGen.Core.Assessment;
using SkillsGen.Core.Fetchers;
using SkillsGen.Core.Generation;
using SkillsGen.Core.Logging;
using SkillsGen.Core.Models;
using SkillsGen.Core.Parsers;
using SkillsGen.Core.PostProcessing;
using SkillsGen.Core.Validation;

namespace SkillsGen.Core.Orchestration;

public class SkillPipelineOrchestrator
{
    private readonly ISkillSourceFetcher _fetcher;
    private readonly ISkillParser _parser;
    private readonly ITriggerParser _triggerParser;
    private readonly ITierAssessor _tierAssessor;
    private readonly ILlmRewriter _llmRewriter;
    private readonly ISkillPageGenerator _pageGenerator;
    private readonly AcrolinxPostProcessor _postProcessor;
    private readonly ISkillPageValidator _validator;
    private readonly ISkillsLogger _logger;
    private readonly string _outputDir;
    private readonly bool _dryRun;
    private readonly bool _force;

    public SkillPipelineOrchestrator(
        ISkillSourceFetcher fetcher,
        ISkillParser parser,
        ITriggerParser triggerParser,
        ITierAssessor tierAssessor,
        ILlmRewriter llmRewriter,
        ISkillPageGenerator pageGenerator,
        AcrolinxPostProcessor postProcessor,
        ISkillPageValidator validator,
        ISkillsLogger logger,
        string outputDir = "./generated-skills",
        bool dryRun = false,
        bool force = false)
    {
        _fetcher = fetcher;
        _parser = parser;
        _triggerParser = triggerParser;
        _tierAssessor = tierAssessor;
        _llmRewriter = llmRewriter;
        _pageGenerator = pageGenerator;
        _postProcessor = postProcessor;
        _validator = validator;
        _logger = logger;
        _outputDir = outputDir;
        _dryRun = dryRun;
        _force = force;
    }

    public async Task<SkillGenerationResult> ProcessSkillAsync(string skillName, string? displayName = null, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Fetch
            var sources = await _fetcher.FetchAsync(skillName, ct);
            if (sources == null)
            {
                _logger.LogError(skillName, "Source files not found");
                return CreateFailResult(skillName, sw, "Source files not found");
            }

            // Parse
            var skillData = _parser.Parse(skillName, sources.SkillMarkdown);
            // Override display name from inventory if provided
            if (!string.IsNullOrEmpty(displayName))
                skillData = skillData with { DisplayName = displayName };
            var triggerData = _triggerParser.Parse(sources.TriggersTestSource);
            _logger.LogParseResult(skillName, skillData.Services.Count, skillData.McpTools.Count, triggerData.ShouldTrigger.Count);

            // Assess
            var tierAssessment = _tierAssessor.Assess(skillData, triggerData);
            _logger.LogTierAssessment(skillName, tierAssessment.Tier, tierAssessment.Rationale);

            // LLM rewrite (optional)
            var llmSw = Stopwatch.StartNew();
            var rewrittenDescription = await _llmRewriter.RewriteIntroAsync(skillName, skillData.Description, ct);
            llmSw.Stop();
            _logger.LogLlmCall(skillName, "RewriteIntro", llmSw.ElapsedMilliseconds);

            var updatedSkillData = skillData with { Description = rewrittenDescription };

            // Build prerequisites from skill data + source file analysis
            var prerequisites = BuildPrerequisites(skillData, sources.FileExtensions);

            // Generate
            var rendered = _pageGenerator.Generate(updatedSkillData, triggerData, tierAssessment, prerequisites);

            // Post-process
            rendered = _postProcessor.Process(rendered);
            var wordCount = rendered.Split(new[] { ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;
            _logger.LogTemplateRender(skillName, wordCount);

            // Validate
            var validation = _validator.Validate(rendered, tierAssessment.Tier, updatedSkillData, triggerData);
            _logger.LogValidation(skillName, validation.IsValid, validation.Errors.Count, validation.Warnings.Count);

            // Write
            string? outputPath = null;
            if (!_dryRun && (validation.IsValid || _force))
            {
                outputPath = WriteOutput(skillName, rendered);
            }

            sw.Stop();
            return new SkillGenerationResult(skillName, tierAssessment.Tier, validation, outputPath, sw.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(skillName, $"Pipeline failed: {ex.Message}", ex);
            sw.Stop();
            return CreateFailResult(skillName, sw, ex.Message);
        }
    }

    public async Task<SkillGenerationReport> ProcessBatchAsync(List<SkillInventoryEntry> skills, CancellationToken ct = default)
    {
        var totalSw = Stopwatch.StartNew();
        var results = new List<SkillGenerationResult>();

        for (int i = 0; i < skills.Count; i++)
        {
            var skill = skills[i];
            _logger.LogInfo($"[{i + 1}/{skills.Count}] {skill.Name} →");
            var itemSw = Stopwatch.StartNew();

            try
            {
                var result = await ProcessSkillAsync(skill.Name, skill.DisplayName, ct);
                results.Add(result);

                var status = result.Validation.IsValid ? "✅ passed" : "❌ failed";
                _logger.LogInfo($"  Tier {result.Tier} → {status} ({result.DurationMs}ms)");
            }
            catch (Exception ex)
            {
                _logger.LogError(skill.Name, $"Batch processing failed: {ex.Message}", ex);
                itemSw.Stop();
                results.Add(CreateFailResult(skill.Name, itemSw, ex.Message));
            }

            // Brief delay between skills to avoid bursting the GitHub API
            if (i < skills.Count - 1)
                await Task.Delay(100, ct);
        }

        totalSw.Stop();
        var succeeded = results.Count(r => r.Validation.IsValid);
        var failed = results.Count - succeeded;
        _logger.LogBatchSummary(results.Count, succeeded, failed, totalSw.ElapsedMilliseconds);

        var report = new SkillGenerationReport(
            results, totalSw.ElapsedMilliseconds,
            "1.0.0", "1.0.0", DateTime.UtcNow);

        if (!_dryRun)
        {
            WriteManifest(report);
        }

        return report;
    }

    private static SkillPrerequisites BuildPrerequisites(SkillData skillData, HashSet<string> fileExtensions)
    {
        // Core tools every skill needs
        var tools = new List<ToolRequirement>
        {
            new("GitHub Copilot", Required: true)
        };

        // Detect additional tools from source file extensions
        if (fileExtensions.Contains(".ps1"))
            tools.Add(new("PowerShell", MinVersion: "7.4", InstallCommand: "winget install Microsoft.PowerShell", Required: true));

        if (fileExtensions.Contains(".js") || fileExtensions.Contains(".ts") || fileExtensions.Contains(".mjs"))
            tools.Add(new("Node.js", MinVersion: "LTS", InstallCommand: "https://nodejs.org", Required: true));

        if (fileExtensions.Contains(".bicep"))
            tools.Add(new("Azure CLI with Bicep", MinVersion: "2.60.0", InstallCommand: "az bicep install", Required: true));

        if (fileExtensions.Contains(".tf") || fileExtensions.Contains(".tfvars"))
            tools.Add(new("Terraform", MinVersion: "1.5", InstallCommand: "https://developer.hashicorp.com/terraform/install", Required: true));

        if (fileExtensions.Contains(".py"))
            tools.Add(new("Python", MinVersion: "3.10", InstallCommand: "https://python.org", Required: true));

        if (fileExtensions.Contains(".sh"))
            tools.Add(new("Bash", Required: true));

        if (fileExtensions.Contains(".yml") || fileExtensions.Contains(".yaml"))
            tools.Add(new("Azure CLI", MinVersion: "2.60.0", InstallCommand: "curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash"));

        // If no special tools detected, still add Azure CLI as recommended
        if (tools.Count == 1)
            tools.Add(new("Azure CLI", MinVersion: "2.60.0", InstallCommand: "curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash"));

        // Detect resource requirements from SKILL.md content
        var resources = new List<ResourceRequirement>();
        var body = skillData.RawBody.ToLowerInvariant();
        if (body.Contains("key vault"))
            resources.Add(new("Azure Key Vault", "Key vault for secrets and certificate management"));
        if (body.Contains("storage account") || body.Contains("blob storage"))
            resources.Add(new("Azure Storage account", "Storage account for blob, file, queue, or table data"));
        if (body.Contains("kubernetes") || body.Contains(" aks "))
            resources.Add(new("Azure Kubernetes Service cluster", "AKS cluster for container orchestration"));
        if (body.Contains("cosmos db") || body.Contains("cosmosdb"))
            resources.Add(new("Azure Cosmos DB account", "Cosmos DB account for NoSQL data"));

        return new SkillPrerequisites
        {
            Azure = new AzureRequirements(),
            Tools = tools,
            Resources = resources
        };
    }

    private string WriteOutput(string skillName, string content)
    {
        Directory.CreateDirectory(_outputDir);
        var path = Path.Combine(_outputDir, $"{skillName}.md");
        File.WriteAllText(path, content);
        return path;
    }

    private void WriteManifest(SkillGenerationReport report)
    {
        Directory.CreateDirectory(_outputDir);
        var path = Path.Combine(_outputDir, "generation-manifest.json");
        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(path, json);
    }

    private static SkillGenerationResult CreateFailResult(string skillName, Stopwatch sw, string error)
    {
        sw.Stop();
        return new SkillGenerationResult(
            skillName, 0,
            new SkillValidationResult(false, [error], [], 0, 0),
            null, sw.ElapsedMilliseconds);
    }
}
