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
            expectedOutputs: ["annotations", "parameters", "tools-raw", "parameter-cli", "example-commands"])
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
            context.McpToolsRoot,
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
            context.McpToolsRoot,
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
            context.McpToolsRoot,
            cancellationToken);
        processResults.Add(rawToolsResult);
        if (!rawToolsResult.Succeeded)
        {
            AddProcessIssue(rawToolsResult, warnings, "Raw tool generation reported issues");
        }

        // Generate CLI parameter files (parameter-cli/)
        var parameterCliResult = await context.ProcessRunner.RunDotNetProjectAsync(
            csharpGeneratorProject,
            ["generate-docs", filteredCli.FilePath, context.OutputPath, "--parameter-cli", "--version", cliVersion],
            context.Request.SkipBuild,
            context.McpToolsRoot,
            cancellationToken);
        processResults.Add(parameterCliResult);
        if (!parameterCliResult.Succeeded)
        {
            AddProcessIssue(parameterCliResult, warnings, "CLI parameter generation reported issues");
        }

        // Generate CLI example command files (example-commands/)
        var exampleCommandsResult = await context.ProcessRunner.RunDotNetProjectAsync(
            csharpGeneratorProject,
            ["generate-docs", filteredCli.FilePath, context.OutputPath, "--example-commands", "--version", cliVersion],
            context.Request.SkipBuild,
            context.McpToolsRoot,
            cancellationToken);
        processResults.Add(exampleCommandsResult);
        if (!exampleCommandsResult.Succeeded)
        {
            AddProcessIssue(exampleCommandsResult, warnings, "CLI example commands generation reported issues");
        }

        var validationIssues = new List<string>();
        if (!context.Request.SkipValidation)
        {
            EnsurePathHasContent(Path.Combine(context.OutputPath, "annotations"), "annotations output", validationIssues);
            EnsurePathHasContent(Path.Combine(context.OutputPath, "parameters"), "parameters output", validationIssues);
            EnsurePathHasContent(rawToolsDirectory, "raw tool output", validationIssues);
            EnsurePathHasContent(Path.Combine(context.OutputPath, "parameter-cli"), "CLI parameter output", validationIssues);
            EnsurePathHasContent(Path.Combine(context.OutputPath, "example-commands"), "CLI example commands output", validationIssues);
        }

        warnings.AddRange(validationIssues);

        var success = annotationsResult.Succeeded
            && parametersResult.Succeeded
            && (rawToolsResult.Succeeded || context.Request.SkipValidation || PathHasContent(rawToolsDirectory))
            && (parameterCliResult.Succeeded || context.Request.SkipValidation || PathHasContent(Path.Combine(context.OutputPath, "parameter-cli")))
            && (exampleCommandsResult.Succeeded || context.Request.SkipValidation || PathHasContent(Path.Combine(context.OutputPath, "example-commands")))
            && validationIssues.Count == 0;

        return BuildResult(context, processResults, success, warnings);
    }
}
