using PipelineRunner.Contracts;
using PipelineRunner.Context;
using PipelineRunner.Services;

namespace PipelineRunner.Steps;

public abstract class NamespaceStepBase : StepDefinition
{
    protected NamespaceStepBase(
        int id,
        string name,
        FailurePolicy failurePolicy,
        IReadOnlyList<int>? dependsOn = null,
        bool requiresAiConfiguration = false,
        bool createsFilteredCliView = false,
        bool usesIsolatedWorkspace = false,
        IReadOnlyList<string>? expectedOutputs = null)
        : base(
            id,
            name,
            StepScope.Namespace,
            failurePolicy,
            dependsOn,
            requiresAiConfiguration: requiresAiConfiguration,
            createsFilteredCliView: createsFilteredCliView,
            usesIsolatedWorkspace: usesIsolatedWorkspace,
            expectedOutputs: expectedOutputs)
    {
    }

    protected (string CurrentNamespace, CliMetadataSnapshot CliOutput, string CliVersion, IReadOnlyList<CliTool> MatchingTools) ResolveTarget(PipelineContext context)
    {
        var currentNamespace = GetCurrentNamespace(context);
        var cliOutput = context.CliOutput ?? throw new InvalidOperationException("CLI metadata must be loaded before executing namespace steps.");
        var cliVersion = context.CliVersion;
        if (string.IsNullOrWhiteSpace(cliVersion))
        {
            throw new InvalidOperationException("CLI version metadata must be loaded before executing namespace steps.");
        }

        var matchingTools = context.TargetMatcher.FindMatches(cliOutput.Tools, currentNamespace);
        return (currentNamespace, cliOutput, cliVersion, matchingTools);
    }

    protected static string GetCurrentNamespace(PipelineContext context)
    {
        if (!context.Items.TryGetValue("Namespace", out var namespaceValue) || namespaceValue is not string currentNamespace)
        {
            throw new InvalidOperationException("Namespace-scoped steps require a current namespace in the pipeline context.");
        }

        return context.TargetMatcher.Normalize(currentNamespace);
    }

    protected static string GetProjectPath(PipelineContext context, string projectName)
        => Path.Combine(context.DocsGenerationRoot, projectName, $"{projectName}.csproj");

    protected ValueTask<FilteredCliFileHandle> CreateFilteredCliFileAsync(
        PipelineContext context,
        CliMetadataSnapshot cliOutput,
        IReadOnlyList<CliTool> matchingTools,
        string tempDirectoryName,
        CancellationToken cancellationToken)
        => context.FilteredCliWriter.WriteAsync(cliOutput, matchingTools, tempDirectoryName, cancellationToken);

    protected static bool PathHasContent(string path)
        => File.Exists(path)
            || (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any());

    protected static void EnsurePathHasContent(string path, string description, ICollection<string> issues)
    {
        if (!PathHasContent(path))
        {
            issues.Add($"Expected {description} at '{path}'.");
        }
    }

    protected static void AddProcessIssue(ProcessExecutionResult processResult, ICollection<string> warnings, string summary)
    {
        warnings.Add($"{summary} (exit code {processResult.ExitCode}).");
        if (!string.IsNullOrWhiteSpace(processResult.StandardError))
        {
            warnings.Add(processResult.StandardError.Trim());
        }
    }

    protected StepResult BuildResult(
        PipelineContext context,
        IReadOnlyCollection<ProcessExecutionResult> processResults,
        bool success,
        IEnumerable<string>? warnings = null,
        IEnumerable<ValidatorResult>? validatorResults = null)
    {
        var resolvedWarnings = warnings?
            .Where(static warning => !string.IsNullOrWhiteSpace(warning))
            .ToArray() ?? Array.Empty<string>();
        var resolvedValidators = validatorResults?.ToArray() ?? Array.Empty<ValidatorResult>();
        var outputs = ExpectedOutputs
            .Select(relativePath => Path.Combine(context.OutputPath, relativePath))
            .ToArray();

        var duration = TimeSpan.FromTicks(processResults.Sum(result => result.Duration.Ticks));
        var commands = processResults.Select(result => result.DisplayCommand).ToArray();

        return new StepResult(success, resolvedWarnings, duration, outputs, commands, resolvedValidators);
    }
}
