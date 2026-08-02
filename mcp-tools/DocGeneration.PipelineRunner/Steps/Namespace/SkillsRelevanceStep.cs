using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using System.Text.RegularExpressions;

namespace PipelineRunner.Steps;

public sealed class SkillsRelevanceStep : NamespaceStepBase
{
    private const string DefaultMinScore = "0.1";

    public SkillsRelevanceStep()
        : base(
            5,
            "Generate skills relevance",
            FailurePolicy.Warn,
            dependsOn: [0],
            expectedOutputs: ["skills-relevance"])
    {
    }

    public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var currentNamespace = GetCurrentNamespace(context);
        var reportStem = SanitizeSkillsReportName(currentNamespace);
        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();
        var artifactFailures = new List<ArtifactFailure>();
        var skillsOutputDirectory = Path.Combine(context.OutputPath, "skills-relevance");

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_TOKEN")))
        {
            Console.WriteLine("  ℹ️  GITHUB_TOKEN not set. Unauthenticated GitHub API rate limits (60 req/hr) apply.");
        }

        var processResult = await context.ProcessRunner.RunDotNetProjectAsync(
            GetProjectPath(context, "DocGeneration.Steps.SkillsRelevance"),
            [
                currentNamespace,
                "--output-path", skillsOutputDirectory,
                "--min-score", DefaultMinScore,
            ],
            context.Request.SkipBuild,
            context.McpToolsRoot,
            cancellationToken);
        processResults.Add(processResult);

        if (!processResult.Succeeded)
        {
            AddProcessIssue(processResult, warnings, "Skills relevance generation failed");
            artifactFailures.Add(CreateArtifactFailure(
                "azure skill",
                $"{reportStem}-skills-relevance.md",
                "Azure skill relevance generation failed for this namespace.",
                warnings,
                [Path.Combine(skillsOutputDirectory, $"{reportStem}-skills-relevance.md")]));
            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        var reportPath = Path.Combine(skillsOutputDirectory, $"{reportStem}-skills-relevance.md");
        if (!File.Exists(reportPath))
        {
            if (HasZeroRelevantSkills(processResult.StandardOutput))
            {
                warnings.Add($"Skills relevance produced zero relevant skills for '{currentNamespace}'.");
                return BuildResult(context, processResults, true, warnings, artifactFailures: artifactFailures);
            }

            warnings.Add($"Expected skills relevance output at '{reportPath}'.");
            artifactFailures.Add(CreateArtifactFailure(
                "azure skill",
                $"{reportStem}-skills-relevance.md",
                "Azure skill relevance output is missing for this namespace.",
                warnings,
                [reportPath]));
            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        return BuildResult(context, processResults, true, warnings, artifactFailures: artifactFailures);
    }

    private static string SanitizeSkillsReportName(string value)
        => Regex.Replace(value.ToLowerInvariant().Replace(' ', '-'), @"[^a-z0-9\-]", string.Empty);

    private static bool HasZeroRelevantSkills(string output)
        => Regex.IsMatch(output ?? string.Empty, @"(?mi)^\s*Relevant skills:\s*0\b");
}
