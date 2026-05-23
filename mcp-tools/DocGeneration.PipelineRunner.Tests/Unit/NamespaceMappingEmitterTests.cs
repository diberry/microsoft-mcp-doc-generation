using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Context;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Steps;
using PipelineRunner.Tests.Fixtures;
using Shared;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="NamespaceMappingEmitter"/> and the integration
/// between <see cref="BootstrapStep"/> and the emitter contract.
/// </summary>
public class NamespaceMappingEmitterTests
{
    // -----------------------------------------------------------------------
    // Phase 1 Unit Tests — NamespaceMappingEmitter in isolation
    // -----------------------------------------------------------------------

    [Fact]
    public async Task AllToolsAccountedExactlyOnce()
    {
        // Arrange — two namespaces, three tools (2 in azurebackup, 1 in storage)
        using var tempDir = new TempDirectory();

        var brandMappings = new List<BrandMappingEntry>
        {
            new("azurebackup", "Azure Backup", "Backup", "azure-backup"),
            new("storage",     "Azure Storage", "Storage", "azure-storage"),
        };

        var cliOutput = BuildCliOutput(
            ("azurebackup create policy", "azurebackup_create_policy"),
            ("azurebackup list vaults",   "azurebackup_list_vaults"),
            ("storage account list",      "storage_account_list"));

        var emitter = new NamespaceMappingEmitter();

        // Act
        await emitter.EmitAsync(brandMappings, cliOutput, "1.0.0", tempDir.Path, CancellationToken.None);

        // Assert — parse output and verify every tool appears exactly once
        var doc = ReadOutputDocument(tempDir.Path);
        var allMappedTools = doc.RootElement.GetProperty("namespaces")
            .EnumerateObject()
            .SelectMany(ns => ns.Value.GetProperty("tools").EnumerateArray().Select(t => t.GetString()!))
            .ToArray();

        Assert.Contains("azurebackup_create_policy", allMappedTools);
        Assert.Contains("azurebackup_list_vaults", allMappedTools);
        Assert.Contains("storage_account_list", allMappedTools);
        Assert.Equal(3, allMappedTools.Length);
        Assert.Equal(allMappedTools.Length, allMappedTools.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task SchemaFieldsPresent()
    {
        // Arrange
        using var tempDir = new TempDirectory();

        var brandMappings = new List<BrandMappingEntry>
        {
            new("compute", "Azure Compute", "Compute", "azure-compute"),
        };

        var cliOutput = BuildCliOutput(("compute list", "compute_list"));
        var emitter = new NamespaceMappingEmitter();

        // Act
        await emitter.EmitAsync(brandMappings, cliOutput, "2.3.4", tempDir.Path, CancellationToken.None);

        // Assert — all required top-level fields are present and non-empty/non-zero
        var doc = ReadOutputDocument(tempDir.Path);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("generated_at", out var generatedAt), "Missing: generated_at");
        Assert.False(string.IsNullOrWhiteSpace(generatedAt.GetString()), "generated_at is empty");

        Assert.True(root.TryGetProperty("source_version", out var version), "Missing: source_version");
        Assert.Equal("2.3.4", version.GetString());

        Assert.True(root.TryGetProperty("namespace_count", out var nsCount), "Missing: namespace_count");
        Assert.Equal(1, nsCount.GetInt32());

        Assert.True(root.TryGetProperty("tool_count", out var toolCount), "Missing: tool_count");
        Assert.Equal(1, toolCount.GetInt32());

        Assert.True(root.TryGetProperty("namespaces", out var namespaces), "Missing: namespaces");

        // Per-namespace entry schema
        var computeNs = namespaces.GetProperty("compute");
        Assert.True(computeNs.TryGetProperty("display_name", out _), "Missing: display_name");
        Assert.True(computeNs.TryGetProperty("file_name", out _),    "Missing: file_name");
        Assert.True(computeNs.TryGetProperty("short_name", out _),   "Missing: short_name");
        Assert.True(computeNs.TryGetProperty("merge_group", out _),  "Missing: merge_group");
        Assert.True(computeNs.TryGetProperty("tools", out _),        "Missing: tools");
    }

    [Fact]
    public async Task MergeGroupPreserved()
    {
        // Arrange — a namespace that is part of a merge group
        using var tempDir = new TempDirectory();

        var brandMappings = new List<BrandMappingEntry>
        {
            new("monitor",   "Azure Monitor",   "Monitor",   "azure-monitor",
                MergeGroup: "azure-monitor", MergeOrder: 1, MergeRole: "primary"),
            new("workbooks", "Azure Workbooks", "Workbooks", "azure-workbooks",
                MergeGroup: "azure-monitor", MergeOrder: 2, MergeRole: "secondary"),
        };

        var cliOutput = BuildCliOutput(
            ("monitor list alerts", "monitor_list_alerts"),
            ("workbooks list",      "workbooks_list"));

        var emitter = new NamespaceMappingEmitter();

        // Act
        await emitter.EmitAsync(brandMappings, cliOutput, "1.0.0", tempDir.Path, CancellationToken.None);

        // Assert — merge_group is preserved in both namespace entries
        var doc = ReadOutputDocument(tempDir.Path);
        var namespaces = doc.RootElement.GetProperty("namespaces");

        var monitorMergeGroup = namespaces.GetProperty("monitor").GetProperty("merge_group").GetString();
        Assert.Equal("azure-monitor", monitorMergeGroup);

        var workbooksMergeGroup = namespaces.GetProperty("workbooks").GetProperty("merge_group").GetString();
        Assert.Equal("azure-monitor", workbooksMergeGroup);
    }

    // -----------------------------------------------------------------------
    // Phase 2 Integration Test — BootstrapStep + emitter
    // -----------------------------------------------------------------------

    [Fact]
    public async Task NotEmittedOnBootstrapFailure()
    {
        // Arrange — CLI metadata loader reports output missing → BootstrapStep returns failure
        var recording = new RecordingNamespaceMappingEmitter();
        var repoRoot = Path.Combine(Path.GetTempPath(), $"nme-failure-test-{Guid.NewGuid():N}");
        var mcpToolsRoot = Path.Combine(repoRoot, "mcp-tools");
        Directory.CreateDirectory(Path.Combine(mcpToolsRoot, "data"));
        Directory.CreateDirectory(Path.Combine(mcpToolsRoot, "azure-mcp"));
        Directory.CreateDirectory(Path.Combine(repoRoot, "test-npm-azure-mcp"));
        File.WriteAllText(Path.Combine(repoRoot, "mcp-doc-generation.sln"), string.Empty);
        File.WriteAllText(Path.Combine(mcpToolsRoot, "data", "brand-to-server-mapping.json"), "[]");
        File.WriteAllText(Path.Combine(mcpToolsRoot, "azure-mcp", "azmcp-commands.md"), "# Commands");

        try
        {
            // DelegatingCliMetadataLoader with outputExists:false forces the "CLI output missing" failure path
            var step = new BootstrapStep(recording);
            var context = new PipelineContext
            {
                Request = new PipelineRequest("compute", [1], ".\\generated-compute",
                    SkipBuild: true, SkipValidation: true, DryRun: false,
                    SkipEnvValidation: true, SkipNpmUpdate: false),
                RepoRoot = repoRoot,
                McpToolsRoot = mcpToolsRoot,
                OutputPath = Path.Combine(repoRoot, "generated-compute"),
                ProcessRunner = new FailingNpmProcessRunner(),
                Workspaces = new WorkspaceManager(),
                CliMetadataLoader = new StubCliMetadataLoader(),   // Always reports missing
                TargetMatcher = new TargetMatcher(),
                FilteredCliWriter = new StubFilteredCliWriter(),
                BuildCoordinator = new StubBuildCoordinator(),
                AiCapabilityProbe = new StubAiCapabilityProbe(),
                Reports = new BufferedReportWriter(),
                PlannedSteps = [step, new AnnotationsParametersRawStep()],
                SelectedNamespaces = Array.Empty<string>(),
            };

            // Act
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            // Assert — step fails and emitter is never called
            Assert.False(result.Success);
            Assert.Equal(0, recording.CallCount);
        }
        finally
        {
            if (Directory.Exists(repoRoot))
            {
                Directory.Delete(repoRoot, recursive: true);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static CliMetadataSnapshot BuildCliOutput(params (string command, string name)[] tools)
    {
        var cliTools = tools
            .Select(t => new CliTool(t.command, t.name, null, default))
            .ToArray();
        return new CliMetadataSnapshot("cli-output.json", default, cliTools);
    }

    private static JsonDocument ReadOutputDocument(string outputPath)
    {
        var filePath = Path.Combine(outputPath, NamespaceMappingEmitter.OutputFileName);
        Assert.True(File.Exists(filePath), $"{NamespaceMappingEmitter.OutputFileName} was not created at '{filePath}'.");
        return JsonDocument.Parse(File.ReadAllText(filePath));
    }

    /// <summary>Records whether <see cref="EmitAsync"/> was invoked.</summary>
    private sealed class RecordingNamespaceMappingEmitter : INamespaceMappingEmitter
    {
        public int CallCount { get; private set; }

        public Task EmitAsync(
            IReadOnlyList<BrandMappingEntry> brandMappings,
            CliMetadataSnapshot cliOutput,
            string cliVersion,
            string outputPath,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Process runner stub that makes npm commands fail, causing BootstrapStep to return early
    /// before it ever reaches the namespace-mapping emission code.
    /// </summary>
    private sealed class FailingNpmProcessRunner : IProcessRunner
    {
        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken cancellationToken)
            => ValueTask.FromResult(new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory, 1, string.Empty, "npm ERR! failed", TimeSpan.Zero));

        public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken cancellationToken)
            => ValueTask.FromResult(new ProcessExecutionResult("dotnet", [], solutionPath, 0, string.Empty, string.Empty, TimeSpan.Zero));

        public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(string projectPath, IEnumerable<string> arguments, bool noBuild, string workingDirectory, CancellationToken cancellationToken)
            => ValueTask.FromResult(new ProcessExecutionResult("dotnet", [], workingDirectory, 0, string.Empty, string.Empty, TimeSpan.Zero));

        public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(string scriptPath, IEnumerable<string> arguments, string workingDirectory, CancellationToken cancellationToken)
            => ValueTask.FromResult(new ProcessExecutionResult("pwsh", [], workingDirectory, 0, string.Empty, string.Empty, TimeSpan.Zero));
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"nme-test-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
