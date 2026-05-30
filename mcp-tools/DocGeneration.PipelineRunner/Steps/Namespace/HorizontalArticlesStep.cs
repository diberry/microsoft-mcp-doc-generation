using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Validation;
using Shared;

namespace PipelineRunner.Steps;

public sealed class HorizontalArticlesStep : NamespaceStepBase
{
    private static readonly UpstreamArtifactResolver UpstreamArtifacts = new();

    public HorizontalArticlesStep()
        : base(
            6,
            "Generate horizontal article",
            FailurePolicy.Fatal,
            dependsOn: [0],
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
        var bootstrapEnvelope = UpstreamArtifacts.TryReadUpstream(context.OutputPath, 0, "bootstrap-pipeline");
        var cliVersionPath = ResolveUpstreamFile(
            context.OutputPath,
            bootstrapEnvelope,
            Path.Combine("cli", "cli-version.json"),
            Path.Combine(context.OutputPath, "cli", "cli-version.json"));
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
            context.McpToolsRoot,
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

    private static string ResolveUpstreamFile(
        string outputPath,
        StepResultFile? envelope,
        string relativeFilePath,
        string fallbackPath)
    {
        if (UpstreamArtifacts.TryResolveOutputFile(outputPath, envelope, relativeFilePath, out var resolvedPath))
        {
            Console.WriteLine(
                $"INFO: Using bootstrap envelope-based resolution for '{relativeFilePath}' at '{resolvedPath}'.");
            return resolvedPath;
        }

        return fallbackPath;
    }
}
