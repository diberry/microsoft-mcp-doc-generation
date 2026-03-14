using PipelineRunner.Contracts;
using PipelineRunner.Context;

namespace PipelineRunner.Steps;

public sealed class ShimStep : StepDefinition
{
    private readonly string _scriptPath;
    private readonly string _targetParameterName;

    public ShimStep(
        int id,
        string name,
        FailurePolicy failurePolicy,
        string scriptPath,
        string targetParameterName,
        IReadOnlyList<int>? dependsOn = null,
        bool requiresAiConfiguration = false,
        bool usesIsolatedWorkspace = false,
        IReadOnlyList<string>? expectedOutputs = null)
        : base(
            id,
            name,
            StepScope.Namespace,
            failurePolicy,
            dependsOn,
            requiresAiConfiguration: requiresAiConfiguration,
            createsFilteredCliView: true,
            usesIsolatedWorkspace: usesIsolatedWorkspace,
            expectedOutputs: expectedOutputs,
            implementation: "Shim")
    {
        _scriptPath = scriptPath;
        _targetParameterName = targetParameterName;
    }

    public override async ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        if (!context.Items.TryGetValue("Namespace", out var namespaceValue) || namespaceValue is not string currentNamespace)
        {
            throw new InvalidOperationException("Namespace-scoped shim steps require a current namespace in the pipeline context.");
        }

        var arguments = new List<string>
        {
            $"-{_targetParameterName}",
            currentNamespace,
            "-OutputPath",
            context.OutputPath,
        };

        if (context.Request.SkipBuild)
        {
            arguments.Add("-SkipBuild");
        }

        if (context.Request.SkipValidation)
        {
            arguments.Add("-SkipValidation");
        }

        var processResult = await context.ProcessRunner.RunPowerShellScriptAsync(
            _scriptPath,
            arguments,
            context.RepoRoot,
            cancellationToken);

        var outputs = ExpectedOutputs
            .Select(relativePath => Path.Combine(context.OutputPath, relativePath))
            .ToArray();

        var warnings = new List<string>();
        if (!processResult.Succeeded && FailurePolicy == FailurePolicy.Warn)
        {
            warnings.Add($"Step {Id} completed with warnings (exit code {processResult.ExitCode}).");
        }

        if (!string.IsNullOrWhiteSpace(processResult.StandardError) && FailurePolicy == FailurePolicy.Warn)
        {
            warnings.Add(processResult.StandardError.Trim());
        }

        return new StepResult(
            processResult.Succeeded,
            warnings,
            processResult.Duration,
            outputs,
            [processResult.DisplayCommand],
            Array.Empty<ValidatorResult>());
    }
}
