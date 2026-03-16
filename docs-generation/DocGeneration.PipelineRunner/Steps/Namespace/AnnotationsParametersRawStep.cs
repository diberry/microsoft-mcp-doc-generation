using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;

namespace PipelineRunner.Steps;

public sealed class AnnotationsParametersRawStep : NamespaceStepBase
{
    public AnnotationsParametersRawStep()
        : base(
            1,
            "Generate annotations, parameters, and raw tools",
            FailurePolicy.Fatal,
            createsFilteredCliView: true,
            expectedOutputs: ["annotations", "parameters", "tools-raw"])
    {
    }

    public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var (_, cliOutput, cliVersion, matchingTools) = ResolveTarget(context);
        var filteredCli = await CreateFilteredCliFileAsync(context, cliOutput, matchingTools, "pipeline-runner-step1", cancellationToken);

        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();
        var csharpGeneratorProject = GetProjectPath(context, "DocGeneration.Steps.AnnotationsParametersRaw.Annotations");
        var rawGeneratorProject = GetProjectPath(context, "DocGeneration.Steps.AnnotationsParametersRaw.RawTools");
        var rawToolsDirectory = Path.Combine(context.OutputPath, "tools-raw");

        var annotationsResult = await context.ProcessRunner.RunDotNetProjectAsync(
            csharpGeneratorProject,
            ["generate-docs", filteredCli.FilePath, context.OutputPath, "--annotations", "--version", cliVersion],
            context.Request.SkipBuild,
            context.DocsGenerationRoot,
            cancellationToken);
        processResults.Add(annotationsResult);
        if (!annotationsResult.Succeeded)
        {
            AddProcessIssue(annotationsResult, warnings, "Annotations generation failed");
            return BuildResult(context, processResults, false, warnings);
        }

        var parametersResult = await context.ProcessRunner.RunDotNetProjectAsync(
            csharpGeneratorProject,
            ["generate-docs", filteredCli.FilePath, context.OutputPath, "--parameters", "--version", cliVersion],
            context.Request.SkipBuild,
            context.DocsGenerationRoot,
            cancellationToken);
        processResults.Add(parametersResult);
        if (!parametersResult.Succeeded)
        {
            AddProcessIssue(parametersResult, warnings, "Parameter generation failed");
            return BuildResult(context, processResults, false, warnings);
        }

        var rawToolsResult = await context.ProcessRunner.RunDotNetProjectAsync(
            rawGeneratorProject,
            [filteredCli.FilePath, rawToolsDirectory, cliVersion],
            context.Request.SkipBuild,
            context.DocsGenerationRoot,
            cancellationToken);
        processResults.Add(rawToolsResult);
        if (!rawToolsResult.Succeeded)
        {
            AddProcessIssue(rawToolsResult, warnings, "Raw tool generation reported issues");
        }

        var validationIssues = new List<string>();
        if (!context.Request.SkipValidation)
        {
            EnsurePathHasContent(Path.Combine(context.OutputPath, "annotations"), "annotations output", validationIssues);
            EnsurePathHasContent(Path.Combine(context.OutputPath, "parameters"), "parameters output", validationIssues);
            EnsurePathHasContent(rawToolsDirectory, "raw tool output", validationIssues);
        }

        warnings.AddRange(validationIssues);

        var success = annotationsResult.Succeeded
            && parametersResult.Succeeded
            && (rawToolsResult.Succeeded || context.Request.SkipValidation || PathHasContent(rawToolsDirectory))
            && validationIssues.Count == 0;

        return BuildResult(context, processResults, success, warnings);
    }
}
