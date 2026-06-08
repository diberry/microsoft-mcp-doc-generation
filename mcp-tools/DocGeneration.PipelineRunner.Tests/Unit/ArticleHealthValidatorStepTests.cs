using System.Text.Json;
using PipelineRunner.Cli;
using PipelineRunner.Contracts;
using PipelineRunner.Context;
using PipelineRunner.Services;
using PipelineRunner.Steps;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="ArticleHealthValidatorStep"/>.
/// Each test wires a fully-resolved <see cref="PipelineContext"/> so the step
/// runs its Execute path against a controlled file system and a
/// <see cref="RecordingProcessRunner"/> or custom stub.
/// </summary>
public class ArticleHealthValidatorStepTests
{
    // ── Step contract ────────────────────────────────────────────────────────

    [Fact]
    public void Step_HasCorrectId()
    {
        var step = new ArticleHealthValidatorStep();
        Assert.Equal(7, step.Id);
    }

    [Fact]
    public void Step_HasNamespaceScope()
    {
        var step = new ArticleHealthValidatorStep();
        Assert.Equal(StepScope.Namespace, step.Scope);
    }

    [Fact]
    public void Step_DependsOnStep4()
    {
        var step = new ArticleHealthValidatorStep();
        Assert.Contains(4, step.DependsOn);
    }

    [Fact]
    public void Step_FailurePolicyIsWarn()
    {
        var step = new ArticleHealthValidatorStep();
        Assert.Equal(FailurePolicy.Warn, step.FailurePolicy);
    }

    // ── No tool-family articles found ────────────────────────────────────────

    [Fact]
    public async Task ValidationScriptRunner_BuildArguments_EmptyArticlePaths_DoesNotThrow()
    {
        var runner = new ValidationScriptRunner(new RecordingProcessRunner());
        var request = new ValidationScriptRequest(
            ScriptPath: "Test-ArticleHealth.ps1",
            RunId: Guid.NewGuid().ToString(),
            Namespace: "storage",
            RepoRoot: Path.GetTempPath(),
            OutputRoot: Path.GetTempPath(),
            OutputJsonPath: Path.Combine(Path.GetTempPath(), "article-health.json"),
            ArticlePaths: [],
            AdditionalArguments: new Dictionary<string, string>());

        // Empty ArticlePaths no longer throws — coverage step uses AdditionalArguments instead
        var result = await runner.RunAsync(request, CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_NoToolFamilyArticles_ReturnsFalseWithWarning()
    {
        var env = SetupTestEnvironment();
        try
        {
            // tool-family directory is intentionally empty
            var step = new ArticleHealthValidatorStep();
            var context = await BuildContextAsync(env, new RecordingProcessRunner());

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("No tool-family article files found"));
        }
        finally { TearDown(env); }
    }

    // ── Script error (process runner returns non-zero exit) ─────────────────

    [Fact]
    public async Task ExecuteAsync_ScriptExitsNonZeroNoArtifact_ReturnsFalseWithScriptError()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateToolFamilyArticle(env, "storage.md");
            // PowerShell exits 1, writes no JSON
            var runner = new FailingProcessRunner(exitCode: 1);
            var step = new ArticleHealthValidatorStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("exit") || w.Contains("Script"));
        }
        finally { TearDown(env); }
    }

    // ── Missing JSON artifact (script succeeds but writes no file) ───────────

    [Fact]
    public async Task ExecuteAsync_ScriptSucceedsButNoJson_ReturnsFalseWithArtifactError()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateToolFamilyArticle(env, "storage.md");
            // Exit 0 but no JSON written
            var runner = new SucceedingProcessRunner();
            var step = new ArticleHealthValidatorStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("artifact") || w.Contains("JSON") || w.Contains("missing") || w.Contains("not written"));
        }
        finally { TearDown(env); }
    }

    // ── Malformed JSON artifact ───────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_MalformedJson_ReturnsFalseWithArtifactError()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateToolFamilyArticle(env, "storage.md");
            var runner = new ArtifactWritingProcessRunner("THIS IS NOT JSON");
            var step = new ArticleHealthValidatorStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("malformed") || w.Contains("JSON") || w.Contains("artifact"));
        }
        finally { TearDown(env); }
    }

    // ── RunId mismatch ────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_RunIdMismatch_ReturnsFalseWithArtifactError()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateToolFamilyArticle(env, "storage.md");
            // Write an artifact with a different runId
            var wrongRunId = Guid.NewGuid().ToString();
            var runner = new ArtifactWritingProcessRunner(BuildValidArtifactJson(wrongRunId, "storage", "warn"));
            var step = new ArticleHealthValidatorStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("mismatch") || w.Contains("run-id") || w.Contains("Run-id"));
        }
        finally { TearDown(env); }
    }

    // ── Successful execution — pass verdict ──────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_PassVerdict_ReturnsTrue()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateToolFamilyArticle(env, "storage.md");
            var runner = new RunIdCapturingProcessRunner("pass");
            var step = new ArticleHealthValidatorStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
        }
        finally { TearDown(env); }
    }

    // ── Warn verdict in warn gate mode ────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WarnVerdict_WarnMode_ReturnsTrue()
    {
        var env = SetupTestEnvironment(gateMode: "warn");
        try
        {
            CreateToolFamilyArticle(env, "storage.md");
            var runner = new RunIdCapturingProcessRunner("warn");
            var step = new ArticleHealthValidatorStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            // In warn mode, warn findings are non-blocking
            Assert.True(result.Success);
        }
        finally { TearDown(env); }
    }

    // ── Warn verdict in block gate mode ───────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WarnVerdict_BlockMode_ReturnsFalse()
    {
        var env = SetupTestEnvironment(gateMode: "block");
        try
        {
            CreateToolFamilyArticle(env, "storage.md");
            var runner = new RunIdCapturingProcessRunner("warn");
            var step = new ArticleHealthValidatorStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            // In block mode, warn findings are blocking
            Assert.False(result.Success);
        }
        finally { TearDown(env); }
    }

    // ── Fail verdict ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_FailVerdict_ReturnsFalse()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateToolFamilyArticle(env, "storage.md");
            var runner = new RunIdCapturingProcessRunner("fail");
            var step = new ArticleHealthValidatorStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("fail"));
        }
        finally { TearDown(env); }
    }

    // ── Artifact written and path recorded in outputs ─────────────────────────

    [Fact]
    public async Task ExecuteAsync_PassVerdict_ArtifactPathInOutputs()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateToolFamilyArticle(env, "storage.md");
            var runner = new RunIdCapturingProcessRunner("pass");
            var step = new ArticleHealthValidatorStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains(result.Outputs, o => o.Contains("article-health.json"));
        }
        finally { TearDown(env); }
    }

    // ── Missing gate config defaults to warn mode ─────────────────────────────

    [Fact]
    public async Task ExecuteAsync_MissingGateConfig_DefaultsToWarnMode()
    {
        var env = SetupTestEnvironment(writeGateConfig: false);
        try
        {
            CreateToolFamilyArticle(env, "storage.md");
            var runner = new RunIdCapturingProcessRunner("warn");
            var step = new ArticleHealthValidatorStep();
            var context = await BuildContextAsync(env, runner);

            // Without gate config, should default to warn mode → warn verdict non-blocking
            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
        }
        finally { TearDown(env); }
    }

    // ── Stale artifact is cleaned before execution ────────────────────────────

    [Fact]
    public async Task ExecuteAsync_StaleArtifactPreExists_IsCleanedBeforeRun()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateToolFamilyArticle(env, "storage.md");

            // Write a stale artifact file before running
            var validationDir = Path.Combine(env.OutputPath, "validation");
            Directory.CreateDirectory(validationDir);
            var staleArtifactPath = Path.Combine(validationDir, "article-health.json");
            File.WriteAllText(staleArtifactPath, "STALE");

            var runner = new RunIdCapturingProcessRunner("pass");
            var step = new ArticleHealthValidatorStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            // Stale file should be gone and replaced by fresh artifact
            Assert.True(result.Success);
            Assert.True(File.Exists(staleArtifactPath));
            Assert.NotEqual("STALE", File.ReadAllText(staleArtifactPath));
        }
        finally { TearDown(env); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private sealed record TestEnvironment(
        string RepoRoot,
        string McpToolsRoot,
        string OutputPath,
        string GateConfigPath);

    private static TestEnvironment SetupTestEnvironment(
        string gateMode = "warn",
        bool writeGateConfig = true)
    {
        var root = Path.Combine(Path.GetTempPath(), $"ah-step-test-{Guid.NewGuid():N}");
        var mcpTools = Path.Combine(root, "mcp-tools");
        var output = Path.Combine(root, "generated-storage");
        var dataDir = Path.Combine(mcpTools, "data");

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(mcpTools);
        Directory.CreateDirectory(Path.Combine(mcpTools, "validation"));
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(dataDir);

        var gateConfigPath = Path.Combine(dataDir, "validation-gate-config.json");
        if (writeGateConfig)
        {
            File.WriteAllText(gateConfigPath, $"{{\"schemaVersion\":\"1.0\",\"gateMode\":\"{gateMode}\"}}");
        }

        // Create a minimal solution file to satisfy the pipeline runner
        File.WriteAllText(Path.Combine(root, "mcp-doc-generation.sln"), string.Empty);

        return new TestEnvironment(root, mcpTools, output, gateConfigPath);
    }

    private static void CreateToolFamilyArticle(TestEnvironment env, string fileName)
    {
        var dir = Path.Combine(env.OutputPath, "tool-family");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, fileName), "# Test Article\n\nContent.");
    }

    private static void TearDown(TestEnvironment env)
    {
        if (Directory.Exists(env.RepoRoot))
        {
            Directory.Delete(env.RepoRoot, true);
        }
    }

    private static async Task<PipelineContext> BuildContextAsync(TestEnvironment env, IProcessRunner runner)
    {
        var contextFactory = new PipelineContextFactory(
            runner,
            new WorkspaceManager(),
            new StubCliMetadataLoader(),
            new TargetMatcher(),
            new StubFilteredCliWriter(),
            new StubBuildCoordinator(),
            new StubAiCapabilityProbe(),
            new BufferedReportWriter(),
            env.RepoRoot);

        var context = await contextFactory.CreateAsync(
            new PipelineRequest("storage", [7], env.OutputPath,
                SkipBuild: true, SkipValidation: false, DryRun: false,
                SkipChangelogGate: true),
            CancellationToken.None);

        context.Items["Namespace"] = "storage";
        return context;
    }

    private static string BuildValidArtifactJson(string runId, string ns, string verdict)
    {
        var obj = new
        {
            schemaVersion = "1.0",
            runId,
            @namespace = ns,
            generatedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            verdict,
            articleFiles = new[] { "storage.md" },
            filesChecked = 1,
            summary = new { pass = verdict == "pass" ? 5 : 4, warn = verdict == "warn" ? 1 : 0, fail = verdict == "fail" ? 1 : 0 },
            checks = new[]
            {
                new { name = "frontmatter.ms.date", status = verdict == "fail" ? "fail" : "pass", detail = "05/26/2026" },
                new { name = "tokens.placeholder-detected", status = verdict == "warn" ? "warn" : "pass", detail = verdict == "warn" ? "Found 1 unresolved placeholder" : "" },
            }
        };
        return JsonSerializer.Serialize(obj);
    }

    // ── Process runner stubs ──────────────────────────────────────────────────

    /// <summary>
    /// Returns a non-zero exit code and writes no JSON artifact.
    /// </summary>
    private sealed class FailingProcessRunner(int exitCode) : IProcessRunner
    {
        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken ct)
            => ValueTask.FromResult(new ProcessExecutionResult(
                spec.FileName, spec.Arguments, spec.WorkingDirectory,
                exitCode, string.Empty, "Script failed with error.", TimeSpan.Zero));

        public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken ct)
            => ValueTask.FromResult(new ProcessExecutionResult("dotnet", [], solutionPath, 0, "", "", TimeSpan.Zero));

        public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(string projectPath, IEnumerable<string> arguments, bool noBuild, string workingDirectory, CancellationToken ct)
            => ValueTask.FromResult(new ProcessExecutionResult("dotnet", [], workingDirectory, 0, "", "", TimeSpan.Zero));

        public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(string scriptPath, IEnumerable<string> arguments, string workingDirectory, CancellationToken ct)
            => ValueTask.FromResult(new ProcessExecutionResult("pwsh", ["-File", scriptPath], workingDirectory, exitCode, string.Empty, "Script failed.", TimeSpan.Zero));
    }

    /// <summary>
    /// Exits 0 but optionally does not write an artifact.
    /// </summary>
    private sealed class SucceedingProcessRunner() : IProcessRunner
    {
        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken ct)
            => ValueTask.FromResult(new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory, 0, "", "", TimeSpan.Zero));

        public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken ct) => RunAsync(new ProcessSpec("dotnet", [], "."), ct);
        public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(string p, IEnumerable<string> a, bool nb, string wd, CancellationToken ct) => RunAsync(new ProcessSpec("dotnet", [], wd), ct);

        public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(string scriptPath, IEnumerable<string> arguments, string workingDirectory, CancellationToken ct)
        {
            // Does not write the artifact — tests the missing-artifact path
            return ValueTask.FromResult(new ProcessExecutionResult("pwsh", ["-File", scriptPath], workingDirectory, 0, "", "", TimeSpan.Zero));
        }
    }

    /// <summary>
    /// Writes a fixed JSON string as the artifact, regardless of content validity.
    /// Used to test malformed JSON and run-id mismatch paths.
    /// </summary>
    private sealed class ArtifactWritingProcessRunner(string artifactContent) : IProcessRunner
    {
        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken ct)
            => ValueTask.FromResult(new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory, 0, "", "", TimeSpan.Zero));

        public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken ct) => RunAsync(new ProcessSpec("dotnet", [], "."), ct);
        public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(string p, IEnumerable<string> a, bool nb, string wd, CancellationToken ct) => RunAsync(new ProcessSpec("dotnet", [], wd), ct);

        public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(string scriptPath, IEnumerable<string> arguments, string workingDirectory, CancellationToken ct)
        {
            var argList = arguments.ToList();
            var outputJsonPath = ExtractOutputJsonPath(argList);
            if (outputJsonPath is not null)
            {
                var dir = Path.GetDirectoryName(outputJsonPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(outputJsonPath, artifactContent);
            }

            return ValueTask.FromResult(new ProcessExecutionResult("pwsh", ["-File", scriptPath], workingDirectory, 0, "", "", TimeSpan.Zero));
        }

        private static string? ExtractOutputJsonPath(List<string> args)
        {
            for (var i = 0; i < args.Count - 1; i++)
            {
                if (args[i] == "-OutputJson") return args[i + 1];
            }

            return null;
        }
    }

    /// <summary>
    /// Captures the <c>-RunId</c> argument from the script invocation and writes a
    /// valid artifact JSON that echoes that same run-id. This ensures the step's
    /// run-id verification logic succeeds.
    /// </summary>
    private sealed class RunIdCapturingProcessRunner(string verdict) : IProcessRunner
    {
        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken ct)
            => ValueTask.FromResult(new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory, 0, "", "", TimeSpan.Zero));

        public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken ct) => RunAsync(new ProcessSpec("dotnet", [], "."), ct);
        public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(string p, IEnumerable<string> a, bool nb, string wd, CancellationToken ct) => RunAsync(new ProcessSpec("dotnet", [], wd), ct);

        public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(string scriptPath, IEnumerable<string> arguments, string workingDirectory, CancellationToken ct)
        {
            var argList = arguments.ToList();
            var runId = ExtractArg(argList, "-RunId") ?? Guid.NewGuid().ToString();
            var ns = ExtractArg(argList, "-Namespace") ?? "storage";
            var outputJsonPath = ExtractArg(argList, "-OutputJson");

            if (outputJsonPath is not null)
            {
                var dir = Path.GetDirectoryName(outputJsonPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(outputJsonPath, BuildValidArtifactJson(runId, ns, verdict));
            }

            return ValueTask.FromResult(new ProcessExecutionResult("pwsh", ["-File", scriptPath], workingDirectory, 0, "", "", TimeSpan.Zero));
        }

        private static string? ExtractArg(List<string> args, string flag)
        {
            for (var i = 0; i < args.Count - 1; i++)
            {
                if (args[i] == flag) return args[i + 1];
            }

            return null;
        }
    }
}



