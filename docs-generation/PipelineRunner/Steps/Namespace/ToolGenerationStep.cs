using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;

namespace PipelineRunner.Steps;

public sealed class ToolGenerationStep : NamespaceStepBase
{
    private const int DefaultMaxTokens = 8000;

    public ToolGenerationStep()
        : base(
            3,
            "Compose and improve tool files",
            FailurePolicy.Fatal,
            dependsOn: [1, 2],
            requiresAiConfiguration: true,
            createsFilteredCliView: true,
            expectedOutputs: ["tools-composed", "tools"])
    {
    }

    public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var (_, cliOutput, _, matchingTools) = ResolveTarget(context);
        _ = await CreateFilteredCliFileAsync(context, cliOutput, matchingTools, "pipeline-runner-step3", cancellationToken);

        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();
        var rawToolsDirectory = Path.Combine(context.OutputPath, "tools-raw");
        var composedToolsDirectory = Path.Combine(context.OutputPath, "tools-composed");
        var improvedToolsDirectory = Path.Combine(context.OutputPath, "tools");
        var annotationsDirectory = Path.Combine(context.OutputPath, "annotations");
        var parametersDirectory = Path.Combine(context.OutputPath, "parameters");
        var examplePromptsDirectory = Path.Combine(context.OutputPath, "example-prompts");

        var missingPrerequisites = new List<string>();
        EnsurePathHasContent(rawToolsDirectory, "raw tool prerequisites", missingPrerequisites);
        EnsurePathHasContent(annotationsDirectory, "annotation prerequisites", missingPrerequisites);
        EnsurePathHasContent(parametersDirectory, "parameter prerequisites", missingPrerequisites);
        EnsurePathHasContent(examplePromptsDirectory, "example prompt prerequisites", missingPrerequisites);
        if (missingPrerequisites.Count > 0)
        {
            return BuildResult(context, processResults, false, missingPrerequisites);
        }

        var composedResult = await context.ProcessRunner.RunDotNetProjectAsync(
            GetProjectPath(context, "ToolGeneration_Composed"),
            [
                rawToolsDirectory,
                composedToolsDirectory,
                annotationsDirectory,
                parametersDirectory,
                examplePromptsDirectory,
            ],
            context.Request.SkipBuild,
            context.DocsGenerationRoot,
            cancellationToken);
        processResults.Add(composedResult);
        if (!composedResult.Succeeded)
        {
            AddProcessIssue(composedResult, warnings, "Composed tool generation failed");
            return BuildResult(context, processResults, false, warnings);
        }

        if (!PathHasContent(composedToolsDirectory))
        {
            warnings.Add($"Expected composed tool output at '{composedToolsDirectory}'.");
            return BuildResult(context, processResults, false, warnings);
        }

        var improvedResult = await context.ProcessRunner.RunDotNetProjectAsync(
            GetProjectPath(context, "ToolGeneration_Improved"),
            [composedToolsDirectory, improvedToolsDirectory, DefaultMaxTokens.ToString()],
            context.Request.SkipBuild,
            context.DocsGenerationRoot,
            cancellationToken);
        processResults.Add(improvedResult);
        if (!improvedResult.Succeeded)
        {
            AddProcessIssue(improvedResult, warnings, "AI-improved tool generation failed");
            return BuildResult(context, processResults, false, warnings);
        }

        if (!context.Request.SkipValidation)
        {
            var outputIssues = new List<string>();
            EnsurePathHasContent(composedToolsDirectory, "composed tool output", outputIssues);
            EnsurePathHasContent(improvedToolsDirectory, "improved tool output", outputIssues);
            warnings.AddRange(outputIssues);
            if (outputIssues.Count > 0)
            {
                return BuildResult(context, processResults, false, warnings);
            }
        }

        return BuildResult(context, processResults, true, warnings);
    }
}
