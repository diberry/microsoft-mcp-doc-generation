using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using System.Text.Json;
using System.Threading.Tasks;

namespace PipelineRunner.Tests.Fixtures;

internal sealed class RecordingProcessRunner : IProcessRunner
{
    public List<ProcessSpec> Invocations { get; } = new();

    public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
    {
        Invocations.Add(spec);
        return ValueTask.FromResult(new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory, 0, string.Empty, string.Empty, TimeSpan.Zero));
    }

    public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken cancellationToken)
        => RunAsync(
            new ProcessSpec(
                "dotnet",
                ["build", solutionPath, "--configuration", "Release", "--verbosity", "quiet"],
                Path.GetDirectoryName(solutionPath) ?? Environment.CurrentDirectory),
            cancellationToken);

    public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(string projectPath, IEnumerable<string> arguments, bool noBuild, string workingDirectory, CancellationToken cancellationToken)
    {
        var invocation = new List<string>
        {
            "run",
            "--project",
            projectPath,
            "--configuration",
            "Release",
        };

        if (noBuild)
        {
            invocation.Add("--no-build");
        }

        invocation.Add("--");
        invocation.AddRange(arguments);
        return RunAsync(new ProcessSpec("dotnet", invocation, workingDirectory), cancellationToken);
    }

    public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(string scriptPath, IEnumerable<string> arguments, string workingDirectory, CancellationToken cancellationToken)
        => RunAsync(new ProcessSpec("pwsh", ["-File", scriptPath, .. arguments], workingDirectory), cancellationToken);
}

internal sealed class BufferedReportWriter : IReportWriter
{
    public List<string> Messages { get; } = new();

    public void Info(string message) => Messages.Add(message);

    public void Warning(string message) => Messages.Add($"WARNING: {message}");

    public void Error(string message) => Messages.Add($"ERROR: {message}");
}

internal sealed class StubBuildCoordinator : IBuildCoordinator
{
    public ValueTask EnsureReadyAsync(string solutionPath, bool skipBuild, IReadOnlyList<string> requiredArtifacts, CancellationToken cancellationToken)
        => ValueTask.CompletedTask;
}

internal sealed class StubAiCapabilityProbe : IAiCapabilityProbe
{
    public ValueTask<AiCapabilityResult> ProbeAsync(string mcpToolsRoot, CancellationToken cancellationToken)
        => ValueTask.FromResult(new AiCapabilityResult(true, Array.Empty<string>()));
}

internal sealed class StubFilteredCliWriter : IFilteredCliWriter
{
    public ValueTask<FilteredCliFileHandle> WriteAsync(CliMetadataSnapshot cliOutput, IReadOnlyList<CliTool> matchingTools, string tempDirectoryName, CancellationToken cancellationToken)
        => ValueTask.FromResult(new FilteredCliFileHandle(Path.GetTempPath(), Path.Combine(Path.GetTempPath(), "cli-output-single-tool.json")));
}

internal sealed class StubCliMetadataLoader : ICliMetadataLoader
{
    public bool CliOutputExists(string outputPath) => false;

    public bool CliVersionExists(string outputPath) => false;

    public bool NamespaceMetadataExists(string outputPath) => false;

    public ValueTask<CliMetadataSnapshot> LoadCliOutputAsync(string outputPath, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public ValueTask<string> LoadCliVersionAsync(string outputPath, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public ValueTask<IReadOnlyList<string>> LoadNamespacesAsync(string outputPath, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}

/// <summary>
/// A changelog gate stub that skips a specific named namespace and processes all others.
/// Use this in smoke tests and pipeline-level tests to avoid real network calls.
/// </summary>
internal sealed class SkipSpecificNamespaceGate(string namespaceToSkip) : IChangelogGate
{
    public Task<ChangelogGateResult> EvaluateAsync(
        string namespaceName,
        string baselineVersion,
        string mcpBranch,
        bool hasExistingArticle,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            string.Equals(namespaceName, namespaceToSkip, StringComparison.OrdinalIgnoreCase)
                ? ChangelogGateResult.Skip($"namespace '{namespaceName}' not in CHANGELOG (test stub)")
                : ChangelogGateResult.Process($"namespace '{namespaceName}' found in CHANGELOG (test stub)"));
    }
}

/// <summary>
/// A brand mapping loader stub that returns a configurable set of entries.
/// </summary>
internal sealed class StubBrandMappingLoader(IReadOnlyList<BrandMappingEntry>? entries = null) : IBrandMappingLoader
{
    private readonly IReadOnlyList<BrandMappingEntry> _entries = entries ?? [];

    public Task<IReadOnlyList<BrandMappingEntry>> LoadAsync(string mcpToolsRoot, CancellationToken cancellationToken)
        => Task.FromResult(_entries);
}

/// <summary>
/// A namespace/global step double that records every execution and the namespace it ran under,
/// and lets a test dictate the <see cref="StepResult"/> it returns via <see cref="Outcome"/>.
///
/// The step reads the active namespace from <c>context.Items["Namespace"]</c> exactly like the
/// real runner sets it, so per-namespace accounting (Executions / ExecutedNamespaces) reflects
/// the runner's real orchestration behaviour rather than a hand-rolled stand-in.
///
/// Constructed with the same metadata surface as a real <see cref="IPipelineStep"/>
/// (Id, Name, Scope, FailurePolicy, DependsOn, MaxRetries) so a mirrored registry is
/// behaviourally comparable to <see cref="StepRegistry.CreateDefault(string)"/>.
/// </summary>
internal sealed class RecordingNamespaceStep : StepDefinition
{
    public RecordingNamespaceStep(
        int id,
        string name,
        StepScope scope,
        FailurePolicy failurePolicy,
        IReadOnlyList<int> dependsOn,
        int maxRetries,
        Func<string?, StepResult>? outcome = null)
        : base(
            id,
            name,
            scope,
            failurePolicy,
            dependsOn: dependsOn,
            maxRetries: maxRetries)
    {
        Outcome = outcome ?? (_ => StepResult.DryRun(Array.Empty<string>()));
    }

    /// <summary>Total number of times this step executed across all namespaces.</summary>
    public int Executions { get; private set; }

    /// <summary>The namespace value (context.Items["Namespace"]) captured on each execution, in order.</summary>
    public List<string> ExecutedNamespaces { get; } = new();

    /// <summary>
    /// The result this step returns. The argument is the active namespace (null for the global phase).
    /// Defaults to a successful dry-run result. Assign to inject a failure or a human-review outcome.
    /// </summary>
    public Func<string?, StepResult> Outcome { get; set; }

    public override ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var ns = context.Items.TryGetValue("Namespace", out var value) ? value as string : null;
        Executions++;
        if (!string.IsNullOrEmpty(ns))
        {
            ExecutedNamespaces.Add(ns);
        }

        return ValueTask.FromResult(Outcome(ns));
    }
}

/// <summary>
/// Builds a set of <see cref="RecordingNamespaceStep"/> doubles whose (Id, Name, Scope,
/// FailurePolicy, DependsOn, MaxRetries) mirror the real production graph produced by
/// <see cref="StepRegistry.CreateDefault(string)"/>. Tests use these doubles to exercise the
/// runtime orchestration path against the SAME dependency edges the product ships, without
/// invoking the real (AI/network) step bodies.
/// </summary>
internal static class MirroredRegistry
{
    public static IReadOnlyList<RecordingNamespaceStep> CreateDoublesMatchingDefault(string scriptsRoot)
    {
        var realSteps = StepRegistry.CreateDefault(scriptsRoot).GetAllSteps();
        return realSteps
            .Select(step => new RecordingNamespaceStep(
                step.Id,
                step.Name,
                step.Scope,
                step.FailurePolicy,
                step.DependsOn,
                step.MaxRetries))
            .ToArray();
    }
}

/// <summary>
/// Minimal <see cref="ICliMetadataLoader"/> that returns canned data (namespaces
/// "compute" and "storage") so the runner can proceed past bootstrap in orchestration tests.
/// Centralized here so multiple test classes can share it.
/// </summary>
internal sealed class StaticCliMetadataLoader : ICliMetadataLoader
{
    private readonly CliMetadataSnapshot _snapshot = CreateSnapshot();

    public bool CliOutputExists(string outputPath) => true;
    public bool CliVersionExists(string outputPath) => true;
    public bool NamespaceMetadataExists(string outputPath) => true;

    public ValueTask<CliMetadataSnapshot> LoadCliOutputAsync(string outputPath, CancellationToken cancellationToken)
        => ValueTask.FromResult(_snapshot);

    public ValueTask<string> LoadCliVersionAsync(string outputPath, CancellationToken cancellationToken)
        => ValueTask.FromResult("1.0.0");

    public ValueTask<IReadOnlyList<string>> LoadNamespacesAsync(string outputPath, CancellationToken cancellationToken)
        => ValueTask.FromResult<IReadOnlyList<string>>(["compute", "storage"]);

    private static CliMetadataSnapshot CreateSnapshot()
    {
        var json = JsonSerializer.Serialize(new
        {
            results = new[]
            {
                new { command = "compute list", name = "compute list", description = "desc" },
                new { command = "storage list", name = "storage list", description = "desc" },
            },
        });

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement.Clone();
        var tools = root.GetProperty("results")
            .EnumerateArray()
            .Select(tool => new CliTool(
                tool.GetProperty("command").GetString() ?? string.Empty,
                tool.GetProperty("name").GetString() ?? string.Empty,
                tool.GetProperty("description").GetString(),
                tool.Clone()))
            .ToArray();

        return new CliMetadataSnapshot(
            Path.Combine(Path.GetTempPath(), $"cli-output-{Guid.NewGuid():N}.json"),
            root,
            tools);
    }
}
