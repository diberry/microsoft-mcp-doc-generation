using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;

namespace PipelineRunner.Steps;

public sealed class ExamplePromptsStep : NamespaceStepBase
{
    public ExamplePromptsStep()
        : base(
            2,
            "Generate example prompts",
            FailurePolicy.Fatal,
            dependsOn: [1],
            requiresAiConfiguration: true,
            createsFilteredCliView: true,
            expectedOutputs: ["example-prompts", "example-prompts-prompts", "example-prompts-raw-output"])
    {
    }

    public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        var (_, cliOutput, cliVersion, matchingTools) = ResolveTarget(context);
        var filteredCli = await CreateFilteredCliFileAsync(context, cliOutput, matchingTools, "pipeline-runner-step2", cancellationToken);

        var processResults = new List<ProcessExecutionResult>();
        var warnings = new List<string>();
        var validatorResults = new List<ValidatorResult>();
        var generatorProject = GetProjectPath(context, "ExamplePromptGeneratorStandalone");

        var generatorArguments = new List<string>
        {
            filteredCli.FilePath,
            context.OutputPath,
            cliVersion,
        };

        var e2ePromptsPath = Path.Combine(context.OutputPath, "e2e-test-prompts", "parsed.json");
        if (File.Exists(e2ePromptsPath))
        {
            generatorArguments.Add("--e2e-prompts");
            generatorArguments.Add(e2ePromptsPath);
        }

        var parameterManifestDirectory = Path.Combine(context.OutputPath, "parameters");
        generatorArguments.Add("--param-manifests");
        generatorArguments.Add(parameterManifestDirectory);

        var generatorResult = await context.ProcessRunner.RunDotNetProjectAsync(
            generatorProject,
            generatorArguments,
            context.Request.SkipBuild,
            context.DocsGenerationRoot,
            cancellationToken);
        processResults.Add(generatorResult);
        if (!generatorResult.Succeeded)
        {
            AddProcessIssue(generatorResult, warnings, "Example prompt generation failed");
            return BuildResult(context, processResults, false, warnings, validatorResults);
        }

        if (!context.Request.SkipValidation)
        {
            var outputIssues = new List<string>();
            EnsurePathHasContent(Path.Combine(context.OutputPath, "example-prompts"), "example prompts output", outputIssues);
            EnsurePathHasContent(Path.Combine(context.OutputPath, "example-prompts-prompts"), "example prompt input output", outputIssues);
            EnsurePathHasContent(Path.Combine(context.OutputPath, "example-prompts-raw-output"), "example prompt raw output", outputIssues);

            warnings.AddRange(outputIssues);
            if (outputIssues.Count > 0)
            {
                return BuildResult(context, processResults, false, warnings, validatorResults);
            }

            var validatorProject = GetProjectPath(context, "ExamplePromptValidator");
            var validatorWarnings = new List<string>();
            var validatorArguments = new List<string>
            {
                "--generated", context.OutputPath,
                "--example-prompts-dir", Path.Combine(context.OutputPath, "example-prompts"),
            };

            if (matchingTools.Count == 1)
            {
                validatorArguments.Add("--tool-command");
                validatorArguments.Add(matchingTools[0].Command);
            }

            var validatorResult = await context.ProcessRunner.RunDotNetProjectAsync(
                validatorProject,
                validatorArguments,
                context.Request.SkipBuild,
                context.DocsGenerationRoot,
                cancellationToken);

            processResults.Add(validatorResult);
            if (!validatorResult.Succeeded)
            {
                AddProcessIssue(validatorResult, validatorWarnings, "Example prompt validation completed with issues");
                warnings.AddRange(validatorWarnings);
            }

            validatorResults.Add(new ValidatorResult(
                "Validate-ExamplePrompts-RequiredParams",
                validatorResult.Succeeded,
                validatorWarnings));
        }

        return BuildResult(context, processResults, true, warnings, validatorResults);
    }
}
