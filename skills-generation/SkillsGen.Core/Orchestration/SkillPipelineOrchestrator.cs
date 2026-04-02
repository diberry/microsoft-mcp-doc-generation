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

            // Build prerequisites
            var prerequisites = BuildPrerequisites(skillData);

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

    private static SkillPrerequisites BuildPrerequisites(SkillData skillData)
    {
        var tools = new List<ToolRequirement>
        {
            new("GitHub Copilot", Required: true),
            new("Azure CLI", MinVersion: "2.60.0", InstallCommand: "curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash")
        };

        return new SkillPrerequisites
        {
            Azure = new AzureRequirements(),
            Tools = tools
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
