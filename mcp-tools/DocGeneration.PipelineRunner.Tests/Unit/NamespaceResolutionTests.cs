using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Registry;
using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Regression tests for namespace resolution — specifically that underscored namespace names
/// (e.g. extension_azqr, extension_cli_generate) are resolved against the CLI namespace list
/// even though TargetMatcher.Normalize replaces '_' with ' '.
///
/// Root cause: ResolveNamespaces normalized the REQUEST but not the CANDIDATES, so
/// "extension_azqr" (request) → "extension azqr" (normalized) failed to match
/// "extension_azqr" (candidate, still with underscore).
/// </summary>
public class NamespaceResolutionTests
{
    /// <summary>
    /// Reproduces the bug: --namespace extension_azqr should resolve against a CLI namespace list
    /// that contains "extension_azqr" (underscore). Without the fix, the pipeline exits with
    /// exit code 64 and logs "ERROR: Unknown namespace 'extension_azqr'".
    /// </summary>
    [Fact]
    public async Task RunAsync_UnderscoredNamespace_ResolvesAgainstCliNamespaceList()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"ns-resolution-underscore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var reportWriter = new BufferedReportWriter();
            var contextFactory = new PipelineContextFactory(
                new RecordingProcessRunner(),
                new WorkspaceManager(),
                new UnderscoredNamespaceCliMetadataLoader(),
                new TargetMatcher(),
                new StubFilteredCliWriter(),
                new StubBuildCoordinator(),
                new StubAiCapabilityProbe(),
                reportWriter,
                repoRoot);

            var executedNamespaces = new List<string>();
            var namespaceStep = new NamespaceCaptureStep(executedNamespaces);
            var runner = new global::PipelineRunner.PipelineRunner(new StepRegistry([namespaceStep]), contextFactory);

            var request = new PipelineRequest(
                "extension_azqr", [1], ".\\generated-extension_azqr",
                SkipBuild: true, SkipValidation: false, DryRun: false, SkipChangelogGate: true);

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            Assert.Contains("extension_azqr", executedNamespaces);
            Assert.DoesNotContain(reportWriter.Messages,
                m => m.Contains("Unknown namespace", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    /// <summary>
    /// Companion regression guard: an invalid namespace still returns the correct error.
    /// Ensures the normalization fix does not accidentally accept namespaces that do not exist.
    /// </summary>
    [Fact]
    public async Task RunAsync_UnknownNamespace_ReturnsErrorExitCode()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"ns-resolution-unknown-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var reportWriter = new BufferedReportWriter();
            var contextFactory = new PipelineContextFactory(
                new RecordingProcessRunner(),
                new WorkspaceManager(),
                new UnderscoredNamespaceCliMetadataLoader(),
                new TargetMatcher(),
                new StubFilteredCliWriter(),
                new StubBuildCoordinator(),
                new StubAiCapabilityProbe(),
                reportWriter,
                repoRoot);

            var runner = new global::PipelineRunner.PipelineRunner(
                new StepRegistry([new NamespaceCaptureStep([])]), contextFactory);

            var request = new PipelineRequest(
                "does_not_exist", [1], ".\\generated-does_not_exist",
                SkipBuild: true, SkipValidation: false, DryRun: false, SkipChangelogGate: true);

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.InvalidArgumentsExitCode, exitCode);
            Assert.Contains(reportWriter.Messages,
                m => m.Contains("Unknown namespace", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    /// <summary>
    /// Issue #608: --namespace extension (parent prefix) should auto-expand to all three
    /// extension sub-namespaces and run the pipeline for each of them.
    /// </summary>
    [Fact]
    public async Task RunAsync_ParentNamespacePrefix_ExpandsToAllSubNamespaces()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"ns-resolution-expand-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var reportWriter = new BufferedReportWriter();
            var contextFactory = new PipelineContextFactory(
                new RecordingProcessRunner(),
                new WorkspaceManager(),
                new UnderscoredNamespaceCliMetadataLoader(),
                new TargetMatcher(),
                new StubFilteredCliWriter(),
                new StubBuildCoordinator(),
                new StubAiCapabilityProbe(),
                reportWriter,
                repoRoot);

            var brandEntries = new[]
            {
                new BrandMappingEntry("extension_azqr", "Azure Compliance Quick Review", "AZQR", "azure-compliance-quick-review", "split"),
                new BrandMappingEntry("extension_cli_generate", "Azure CLI Extension", "CLI Extension", "azure-cli-extension-generate", "split"),
                new BrandMappingEntry("extension_cli_install", "Azure CLI Extension", "CLI Extension", "azure-cli-extension-install", "split"),
            };

            var executedNamespaces = new List<string>();
            var namespaceStep = new NamespaceCaptureStep(executedNamespaces);
            var runner = new global::PipelineRunner.PipelineRunner(
                new StepRegistry([namespaceStep]),
                contextFactory,
                brandMappingLoader: new StubBrandMappingLoader(brandEntries));

            var request = new PipelineRequest(
                "extension", [1], ".\\generated-extension",
                SkipBuild: true, SkipValidation: false, DryRun: false, SkipChangelogGate: true);

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            Assert.Equal(3, executedNamespaces.Count);
            Assert.Contains("extension_azqr", executedNamespaces);
            Assert.Contains("extension_cli_generate", executedNamespaces);
            Assert.Contains("extension_cli_install", executedNamespaces);
            Assert.Contains(reportWriter.Messages,
                m => m.Contains("expanded", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    /// <summary>
    /// Issue #608: --namespace all should run all available CLI namespaces.
    /// </summary>
    [Fact]
    public async Task RunAsync_AllKeyword_RunsAllCliNamespaces()
    {
        var repoRoot = Path.Combine(Path.GetTempPath(), $"ns-resolution-all-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(repoRoot, "mcp-tools", "scripts"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);

        try
        {
            var reportWriter = new BufferedReportWriter();
            var contextFactory = new PipelineContextFactory(
                new RecordingProcessRunner(),
                new WorkspaceManager(),
                new UnderscoredNamespaceCliMetadataLoader(),
                new TargetMatcher(),
                new StubFilteredCliWriter(),
                new StubBuildCoordinator(),
                new StubAiCapabilityProbe(),
                reportWriter,
                repoRoot);

            var executedNamespaces = new List<string>();
            var namespaceStep = new NamespaceCaptureStep(executedNamespaces);
            var runner = new global::PipelineRunner.PipelineRunner(
                new StepRegistry([namespaceStep]),
                contextFactory,
                brandMappingLoader: new StubBrandMappingLoader());

            var request = new PipelineRequest(
                "all", [1], ".\\generated-all",
                SkipBuild: true, SkipValidation: false, DryRun: false, SkipChangelogGate: true);

            var exitCode = await runner.RunAsync(request, CancellationToken.None);

            // UnderscoredNamespaceCliMetadataLoader returns 3 extension namespaces
            Assert.Equal(global::PipelineRunner.PipelineRunner.SuccessExitCode, exitCode);
            Assert.Equal(3, executedNamespaces.Count);
        }
        finally
        {
            Directory.Delete(repoRoot, recursive: true);
        }
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed class NamespaceCaptureStep(List<string> captured)
        : StepDefinition(1, "Capture namespace", StepScope.Namespace, FailurePolicy.Fatal, requiresCliOutput: false, requiresCliVersion: false)
    {
        public override ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
        {
            if (context.Items.TryGetValue("Namespace", out var ns))
                captured.Add(ns?.ToString() ?? string.Empty);
            return ValueTask.FromResult(StepResult.DryRun(Array.Empty<string>()));
        }
    }

    /// <summary>
    /// Simulates the real CLI namespace list for extension namespaces (names with underscores).
    /// </summary>
    private sealed class UnderscoredNamespaceCliMetadataLoader : ICliMetadataLoader
    {
        private static readonly string[] Namespaces = ["extension_azqr", "extension_cli_generate", "extension_cli_install"];

        public bool CliOutputExists(string outputPath) => true;
        public bool CliVersionExists(string outputPath) => true;
        public bool NamespaceMetadataExists(string outputPath) => true;

        public ValueTask<CliMetadataSnapshot> LoadCliOutputAsync(string outputPath, CancellationToken cancellationToken)
            => ValueTask.FromResult(CreateSnapshot());

        public ValueTask<string> LoadCliVersionAsync(string outputPath, CancellationToken cancellationToken)
            => ValueTask.FromResult("1.2.3");

        public ValueTask<IReadOnlyList<string>> LoadNamespacesAsync(string outputPath, CancellationToken cancellationToken)
            => ValueTask.FromResult<IReadOnlyList<string>>(Namespaces);

        private static CliMetadataSnapshot CreateSnapshot()
        {
            var json = JsonSerializer.Serialize(new
            {
                results = Namespaces.Select(ns => new
                {
                    // CLI commands use space (the normalized form)
                    command = ns.Replace('_', ' '),
                    name = ns,
                    description = $"Description for {ns}",
                }),
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
                Path.Combine(Path.GetTempPath(), $"cli-output-{Guid.NewGuid():N}.json"), root, tools);
        }
    }
}
