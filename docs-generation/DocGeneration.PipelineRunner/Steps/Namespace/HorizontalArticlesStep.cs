using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Validation;

namespace PipelineRunner.Steps;

public sealed class HorizontalArticlesStep : NamespaceStepBase
{
    public HorizontalArticlesStep()
        : base(
            6,
            "Generate horizontal article",
            FailurePolicy.Fatal,
            requiresAiConfiguration: true,
            createsFilteredCliView: true,
            expectedOutputs: ["horizontal-articles"],
            postValidators: [new HorizontalArticleOutputValidator()])
    {
    }

    public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var (currentNamespace, cliOutput, _, matchingTools) = ResolveTarget(context);
        _ = await CreateFilteredCliFileAsync(context, cliOutput, matchingTools, "pipeline-runner-step6", cancellationToken);

        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();
        var artifactFailures = new List<ArtifactFailure>();
        var cliVersionPath = Path.Combine(context.OutputPath, "cli", "cli-version.json");
        if (!context.Request.SkipValidation && !File.Exists(cliVersionPath))
        {
            warnings.Add($"CLI version file not found at '{cliVersionPath}'.");
            artifactFailures.Add(CreateHorizontalFailure(context, currentNamespace, warnings));
            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        var processResult = await context.ProcessRunner.RunDotNetProjectAsync(
            GetProjectPath(context, "DocGeneration.Steps.HorizontalArticles"),
            ["--single-service", currentNamespace, "--output-path", context.OutputPath, "--transform"],
            context.Request.SkipBuild,
            context.DocsGenerationRoot,
            cancellationToken);
        processResults.Add(processResult);
        if (!processResult.Succeeded)
        {
            AddProcessIssue(processResult, warnings, "Horizontal article generation failed");
            artifactFailures.Add(CreateHorizontalFailure(context, currentNamespace, warnings));
            return BuildResult(context, processResults, false, warnings, artifactFailures: artifactFailures);
        }

        var articleDirectory = Path.Combine(context.OutputPath, "horizontal-articles");
        var articlePath = Path.Combine(articleDirectory, $"horizontal-article-{currentNamespace}.md");
        var errorPath = Path.Combine(articleDirectory, $"error-{currentNamespace}.txt");
        var aiErrorPath = Path.Combine(articleDirectory, $"error-{currentNamespace}-airesponse.txt");

        var success = true;
        if (File.Exists(errorPath) || File.Exists(aiErrorPath))
        {
            success = false;
            warnings.Add($"Horizontal article generation produced an error artifact for '{currentNamespace}'.");
        }

        if (!File.Exists(articlePath))
        {
            warnings.Add($"Expected horizontal article output at '{articlePath}'.");
            success = false;
        }

        if (!success)
        {
            artifactFailures.Add(CreateHorizontalFailure(context, currentNamespace, warnings));
        }

        return BuildResult(context, processResults, success, warnings, artifactFailures: artifactFailures);
    }

    private static ArtifactFailure CreateHorizontalFailure(PipelineContext context, string currentNamespace, IEnumerable<string> details)
    {
        var articleDirectory = Path.Combine(context.OutputPath, "horizontal-articles");
        return CreateArtifactFailure(
            "horizontal article",
            $"horizontal-article-{currentNamespace}.md",
            "Horizontal article generation failed for this namespace.",
            details,
            [
                Path.Combine(articleDirectory, $"horizontal-article-{currentNamespace}.md"),
                Path.Combine(articleDirectory, $"error-{currentNamespace}.txt"),
                Path.Combine(articleDirectory, $"error-{currentNamespace}-airesponse.txt"),
            ]);
    }
}
