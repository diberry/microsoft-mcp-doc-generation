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

/// <summary>
/// Canonical <see cref="StepResult"/> shapes for ADDENDUM B (Ellis FAIL remediation, #813 Step 2).
///
/// These reproduce the REAL runtime failure shapes proven out by the frozen beta.34 corpus — shapes the
/// pre-existing <see cref="DependencySuppressionTests"/> doubles could NOT express because every test
/// there only ever injected <c>Success=false</c>. In particular <see cref="ValidationAfterRetriesFailure"/>
/// is the D1 shape (Ellis BLOCKING-1): a Fatal step that returns <c>Success=true</c> yet carries a
/// non-empty <see cref="StepResult.ArtifactFailures"/> list. Varied Azure services are used across call
/// sites per the Universal Design Principle; the neutral defaults below are overridable.
/// </summary>
internal static class StepOutcomes
{
    /// <summary>
    /// The real Step-2 (example-prompts) failure shape: validation failed after automatic retries, so
    /// the generator returns <c>Success=true</c> (it DID produce output) but surfaces the failure as one
    /// <see cref="ArtifactFailure"/> plus one failed <see cref="ValidatorResult"/>. Under the corrected
    /// root predicate (AD-029 §A2, clause C2) the non-empty ArtifactFailures makes this a fatal root even
    /// though its mapped exit code is <c>SuccessExitCode</c>. Reused on a Warn-policy step by T39 to prove
    /// the same shape is NON-fatal under a warning policy.
    /// </summary>
    internal static StepResult ValidationAfterRetriesFailure(string toolCommand = "storage account create")
        => new(
            Success: true,
            Warnings: new[] { $"Example prompt validation failed for '{toolCommand}' after automatic retries." },
            Duration: TimeSpan.Zero,
            Outputs: Array.Empty<string>(),
            ProcessInvocations: Array.Empty<string>(),
            ValidatorResults: new[]
            {
                new ValidatorResult(
                    "Validate-ExamplePrompts-RequiredParams",
                    false,
                    new[] { $"Required parameters missing from example prompts for '{toolCommand}'." }),
            },
            ArtifactFailures: new[]
            {
                ArtifactFailure.Create(
                    "tool",
                    toolCommand,
                    "Example prompt validation failed for this tool after automatic retries.",
                    new[] { $"Required-parameter coverage divergence for '{toolCommand}'." }),
            });

    /// <summary>
    /// A dependent step that genuinely executes and fails, surfacing its OWN artifact failure. Used by
    /// T35 to retire finding F1: were a transitive dependent (incorrectly) executed instead of suppressed,
    /// THIS outcome would persist a step-scoped critical-failure JSON — so the test's "no dependent JSON"
    /// assertion is genuinely discriminating rather than vacuously true.
    /// </summary>
    internal static StepResult FailingDependentWithArtifacts(string artifactName = "storage")
        => new(
            Success: false,
            Warnings: new[] { $"Tool-family assembly failed for '{artifactName}'." },
            Duration: TimeSpan.Zero,
            Outputs: Array.Empty<string>(),
            ProcessInvocations: Array.Empty<string>(),
            ValidatorResults: Array.Empty<ValidatorResult>(),
            ArtifactFailures: new[]
            {
                ArtifactFailure.Create(
                    "tool family",
                    artifactName,
                    "Post-assembly validation failed for this tool-family article.",
                    new[] { $"Tool-family article for '{artifactName}' failed post-assembly checks." }),
            });

    /// <summary>
    /// The pre-AI gate "non-fatal skip" shape: the step returns <c>Success=true</c> with a FAILED
    /// <see cref="ValidatorResult"/> but an EMPTY <see cref="StepResult.ArtifactFailures"/> list. Under the
    /// corrected predicate (AD-029 §A2) this is deliberately NON-fatal because clause C2 keys on
    /// ArtifactFailures, not on validator results. Used by T40 as the discriminator against mutation M36
    /// (which would wrongly re-key C2 onto validator results and make this a root).
    /// </summary>
    internal static StepResult PreAiSkipNonFatalOutcome(string validatorId = "pre-ai-validation")
        => new(
            Success: true,
            Warnings: new[] { "Pre-AI validation gate skipped this step (non-fatal)." },
            Duration: TimeSpan.Zero,
            Outputs: Array.Empty<string>(),
            ProcessInvocations: Array.Empty<string>(),
            ValidatorResults: new[]
            {
                new ValidatorResult(validatorId, false, new[] { "Pre-AI gate reported a non-fatal skip." }),
            },
            ArtifactFailures: Array.Empty<ArtifactFailure>());
}

/// <summary>
/// READ-ONLY reader over the frozen beta.34 baseline corpus (the real Step-1 fixtures owned by
/// <c>DocGeneration.Baseline.Beta34.Tests</c>). ADDENDUM B / T33 replays these historical failures
/// against the corrected root predicate to prove the D1 regression (Ellis BLOCKING-1): 16 of 17 Step-2
/// roots return <c>Success=true</c> and are therefore invisible to an exit-code-only predicate.
///
/// The corpus is resolved by walking up from the test assembly location to the repo root
/// (<c>mcp-doc-generation.sln</c>), then into the Baseline.Beta34 Fixtures directory. This project takes
/// NO project reference on the frozen baseline test project (that would couple two test projects); it
/// reads the JSON fixtures directly and NEVER writes to them.
/// </summary>
internal static class Beta34Corpus
{
    private const string FixturesRelativePath = "mcp-tools/DocGeneration.Baseline.Beta34.Tests/Fixtures";

    /// <summary>A single manifest record, enriched with its sanitized file's validatorResults count.</summary>
    internal sealed record Entry(
        string StableId,
        string Namespace,
        int StepId,
        string ChainRole,
        string ArtifactName,
        IReadOnlyList<string> UpstreamStableIds,
        int ValidatorResultCount)
    {
        /// <summary>
        /// True when the sanitized record carries validator results. This reconstructs the runtime
        /// <c>Success</c> the step returned: validation ran and failed after retries ⇒ <c>Success=true</c>
        /// (the D1 shape); no validator results ⇒ the generator itself failed ⇒ <c>Success=false</c>.
        /// </summary>
        internal bool HasValidatorResults => ValidatorResultCount > 0;
    }

    internal sealed record AccountingBlock(
        int Step2Records,
        int Step4Records,
        int DependentRecords,
        int DependencyLinks,
        int ChainRoleRoot,
        int ChainRoleCascade);

    internal sealed record Snapshot(IReadOnlyList<Entry> Records, AccountingBlock Accounting);

    internal static Snapshot Load()
    {
        var fixturesDir = ResolveFixturesDir();
        var manifestPath = Path.Combine(fixturesDir, "beta34-baseline-manifest.json");
        using var manifest = JsonDocument.Parse(StripBom(File.ReadAllText(manifestPath)));
        var root = manifest.RootElement;

        var records = new List<Entry>();
        foreach (var el in root.GetProperty("records").EnumerateArray())
        {
            var sanitizedRelative = el.GetProperty("sanitizedRelativePath").GetString() ?? string.Empty;
            var validatorCount = CountValidatorResults(Path.Combine(fixturesDir, sanitizedRelative));
            var upstream = el.GetProperty("upstreamStableIds")
                .EnumerateArray()
                .Select(u => u.GetString() ?? string.Empty)
                .ToArray();

            records.Add(new Entry(
                el.GetProperty("stableId").GetString() ?? string.Empty,
                el.GetProperty("namespace").GetString() ?? string.Empty,
                el.GetProperty("stepId").GetInt32(),
                el.GetProperty("chainRole").GetString() ?? string.Empty,
                el.GetProperty("artifactName").GetString() ?? string.Empty,
                upstream,
                validatorCount));
        }

        var acc = root.GetProperty("accounting");
        var chainRoleCounts = acc.GetProperty("chainRoleCounts");
        var accounting = new AccountingBlock(
            acc.GetProperty("step2Records").GetInt32(),
            acc.GetProperty("step4Records").GetInt32(),
            acc.GetProperty("dependentRecords").GetInt32(),
            acc.GetProperty("dependencyLinks").GetInt32(),
            chainRoleCounts.GetProperty("root").GetInt32(),
            chainRoleCounts.GetProperty("cascade").GetInt32());

        return new Snapshot(records, accounting);
    }

    /// <summary>Parses the step id embedded in a stable id of the form <c>{ns}.{stepId:D2}.{slug}</c>.</summary>
    internal static int StepIdOf(string stableId)
    {
        var parts = stableId.Split('.');
        return parts.Length >= 2 && int.TryParse(parts[1], out var id) ? id : -1;
    }

    private static int CountValidatorResults(string sanitizedFilePath)
    {
        using var doc = JsonDocument.Parse(StripBom(File.ReadAllText(sanitizedFilePath)));
        return doc.RootElement.TryGetProperty("validatorResults", out var vr) && vr.ValueKind == JsonValueKind.Array
            ? vr.GetArrayLength()
            : 0;
    }

    private static string ResolveFixturesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "mcp-doc-generation.sln")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new DirectoryNotFoundException(
                $"Could not locate repo root (mcp-doc-generation.sln) above '{AppContext.BaseDirectory}'.");
        }

        var fixtures = Path.Combine(dir.FullName, FixturesRelativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(fixtures))
        {
            throw new DirectoryNotFoundException($"Beta.34 fixtures not found at '{fixtures}'.");
        }

        return fixtures;
    }

    private static string StripBom(string text)
        => text.Length > 0 && text[0] == '\uFEFF' ? text[1..] : text;
}
