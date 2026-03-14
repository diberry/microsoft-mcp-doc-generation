using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;

namespace PipelineRunner.Steps;

public sealed class SkillsRelevanceStep : NamespaceStepBase
{
    private const string DefaultMinScore = "0.1";

    public SkillsRelevanceStep()
        : base(
            5,
            "Generate skills relevance",
            FailurePolicy.Warn,
            expectedOutputs: ["skills-relevance"])
    {
    }

    public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var currentNamespace = GetCurrentNamespace(context);
        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();
        var skillsOutputDirectory = Path.Combine(context.OutputPath, "skills-relevance");

        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("GITHUB_TOKEN")))
        {
            warnings.Add("GITHUB_TOKEN not set. Unauthenticated GitHub API rate limits (60 req/hr) apply.");
        }

        var processResult = await context.ProcessRunner.RunDotNetProjectAsync(
            GetProjectPath(context, "SkillsRelevance"),
            [
                currentNamespace,
                "--output-path", skillsOutputDirectory,
                "--min-score", DefaultMinScore,
            ],
            context.Request.SkipBuild,
            context.DocsGenerationRoot,
            cancellationToken);
        processResults.Add(processResult);

        if (!processResult.Succeeded)
        {
            AddProcessIssue(processResult, warnings, "Skills relevance generation failed");
            return BuildResult(context, processResults, false, warnings);
        }

        var reportPath = Path.Combine(skillsOutputDirectory, $"{currentNamespace}-skills-relevance.md");
        if (!File.Exists(reportPath))
        {
            warnings.Add($"Expected skills relevance output at '{reportPath}'.");
            return BuildResult(context, processResults, false, warnings);
        }

        return BuildResult(context, processResults, true, warnings);
    }
}
