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
/// Unit tests for <see cref="CoverageAuditStep"/>.
/// Mirrors the <see cref="ArticleHealthValidatorStepTests"/> pattern with controlled
/// file system and custom <see cref="IProcessRunner"/> stubs.
/// </summary>
public class CoverageAuditStepTests
{
    // ── Step contract ────────────────────────────────────────────────────────

    [Fact]
    public void Step_HasCorrectId()
    {
        var step = new CoverageAuditStep();
        Assert.Equal(8, step.Id);
    }

    [Fact]
    public void Step_HasNamespaceScope()
    {
        var step = new CoverageAuditStep();
        Assert.Equal(StepScope.Namespace, step.Scope);
    }

    [Fact]
    public void Step_DependsOnStep0And4()
    {
        var step = new CoverageAuditStep();
        Assert.Contains(0, step.DependsOn);
        Assert.Contains(4, step.DependsOn);
    }

    [Fact]
    public void Step_FailurePolicyIsWarn()
    {
        var step = new CoverageAuditStep();
        Assert.Equal(FailurePolicy.Warn, step.FailurePolicy);
    }

    // ── Missing cli-output.json ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_NoCliOutput_ReturnsFalseWithWarning()
    {
        var env = SetupTestEnvironment();
        try
        {
            // cli/cli-output.json is intentionally missing
            CreateArticlesDirectory(env);

            var step = new CoverageAuditStep();
            var context = await BuildContextAsync(env, new RecordingProcessRunner());

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("CLI output not found") || w.Contains("cli-output.json"));
        }
        finally { TearDown(env); }
    }

    // ── Missing articles directory ───────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_NoArticlesDir_ReturnsFalseWithWarning()
    {
        var env = SetupTestEnvironment();
        try
        {
            // Create cli-output.json but no tool-family directory
            CreateCliOutput(env);

            var step = new CoverageAuditStep();
            var context = await BuildContextAsync(env, new RecordingProcessRunner());

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("Articles directory not found") || w.Contains("tool-family"));
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
            CreateCliOutput(env);
            CreateArticlesDirectory(env);

            var runner = new FailingProcessRunner(exitCode: 1);
            var step = new CoverageAuditStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("exit") || w.Contains("Script"));
        }
        finally { TearDown(env); }
    }

    // ── Script succeeds but no artifact ──────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ScriptSucceedsButNoJson_ReturnsFalseWithArtifactError()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateCliOutput(env);
            CreateArticlesDirectory(env);

            var runner = new SucceedingProcessRunner();
            var step = new CoverageAuditStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("artifact") || w.Contains("JSON") || w.Contains("not written"));
        }
        finally { TearDown(env); }
    }

    // ── Malformed JSON artifact ──────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_MalformedJson_ReturnsFalseWithArtifactError()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateCliOutput(env);
            CreateArticlesDirectory(env);

            var runner = new ArtifactWritingProcessRunner("NOT VALID JSON!!!");
            var step = new CoverageAuditStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("malformed") || w.Contains("JSON") || w.Contains("artifact"));
        }
        finally { TearDown(env); }
    }

    // ── Pass verdict ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_PassVerdict_ReturnsTrue()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateCliOutput(env);
            CreateArticlesDirectory(env);

            var runner = new RunIdCapturingProcessRunner("pass");
            var step = new CoverageAuditStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
        }
        finally { TearDown(env); }
    }

    // ── Warn verdict in warn gate mode ───────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WarnVerdict_WarnMode_ReturnsTrue()
    {
        var env = SetupTestEnvironment(gateMode: "warn");
        try
        {
            CreateCliOutput(env);
            CreateArticlesDirectory(env);

            var runner = new RunIdCapturingProcessRunner("warn");
            var step = new CoverageAuditStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
        }
        finally { TearDown(env); }
    }

    // ── Warn verdict in block gate mode ──────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WarnVerdict_BlockMode_ReturnsFalse()
    {
        var env = SetupTestEnvironment(gateMode: "block");
        try
        {
            CreateCliOutput(env);
            CreateArticlesDirectory(env);

            var runner = new RunIdCapturingProcessRunner("warn");
            var step = new CoverageAuditStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
        }
        finally { TearDown(env); }
    }

    // ── Fail verdict ─────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_FailVerdict_ReturnsFalse()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateCliOutput(env);
            CreateArticlesDirectory(env);

            var runner = new RunIdCapturingProcessRunner("fail");
            var step = new CoverageAuditStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains(result.Warnings, w => w.Contains("fail") || w.Contains("missing tools"));
        }
        finally { TearDown(env); }
    }

    // ── Artifact path in outputs ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_PassVerdict_ArtifactPathInOutputs()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateCliOutput(env);
            CreateArticlesDirectory(env);

            var runner = new RunIdCapturingProcessRunner("pass");
            var step = new CoverageAuditStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains(result.Outputs, o => o.Contains("coverage-audit.json"));
        }
        finally { TearDown(env); }
    }

    // ── Missing gate config defaults to warn mode ────────────────────────────

    [Fact]
    public async Task ExecuteAsync_MissingGateConfig_DefaultsToWarnMode()
    {
        var env = SetupTestEnvironment(writeGateConfig: false);
        try
        {
            CreateCliOutput(env);
            CreateArticlesDirectory(env);

            var runner = new RunIdCapturingProcessRunner("warn");
            var step = new CoverageAuditStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            // Without gate config, defaults to warn mode → warn verdict non-blocking
            Assert.True(result.Success);
        }
        finally { TearDown(env); }
    }

    // ── Stale artifact cleaned before execution ──────────────────────────────

    [Fact]
    public async Task ExecuteAsync_StaleArtifactPreExists_IsCleanedBeforeRun()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateCliOutput(env);
            CreateArticlesDirectory(env);

            var validationDir = Path.Combine(env.OutputPath, "validation");
            Directory.CreateDirectory(validationDir);
            var staleArtifactPath = Path.Combine(validationDir, "coverage-audit.json");
            File.WriteAllText(staleArtifactPath, "STALE");

            var runner = new RunIdCapturingProcessRunner("pass");
            var step = new CoverageAuditStep();
            var context = await BuildContextAsync(env, runner);

            var result = await step.ExecuteAsync(context, CancellationToken.None);

            Assert.True(result.Success);
            Assert.True(File.Exists(staleArtifactPath));
            Assert.NotEqual("STALE", File.ReadAllText(staleArtifactPath));
        }
        finally { TearDown(env); }
    }

    // ── Validation summary markdown is written ───────────────────────────────

    [Fact]
    public async Task ExecuteAsync_PassVerdict_WritesSummaryMarkdown()
    {
        var env = SetupTestEnvironment();
        try
        {
            CreateCliOutput(env);
            CreateArticlesDirectory(env);

            var runner = new RunIdCapturingProcessRunner("pass");
            var step = new CoverageAuditStep();
            var context = await BuildContextAsync(env, runner);

            await step.ExecuteAsync(context, CancellationToken.None);

            var summaryPath = Path.Combine(env.OutputPath, "validation", "validation-summary.md");
            Assert.True(File.Exists(summaryPath));
            var content = File.ReadAllText(summaryPath);
            Assert.Contains("Coverage Audit", content);
            Assert.Contains("pass", content);
        }
        finally { TearDown(env); }
    }

    // ── ValidationScriptRunner.BuildArguments tests ──────────────────────────

    [Fact]
    public void BuildArguments_NullArticlePaths_WithAdditionalArgs_EmitsOnlyAdditionalArgs()
    {
        var request = new ValidationScriptRequest(
            ScriptPath: "Scan-McpToolCoverage.ps1",
            RunId: "test-run-id",
            Namespace: "storage",
            RepoRoot: "/repo",
            OutputRoot: "/output",
            OutputJsonPath: "/output/validation/coverage-audit.json",
            AdditionalArguments: new Dictionary<string, string>
            {
                ["-ToolsJsonPath"] = "/output/cli/cli-output.json",
                ["-ArticlesDir"] = "/output/tool-family"
            });

        var args = ValidationScriptRunner.BuildArguments(request);

        Assert.Contains("-ToolsJsonPath", args);
        Assert.Contains("/output/cli/cli-output.json", args);
        Assert.Contains("-ArticlesDir", args);
        Assert.Contains("/output/tool-family", args);
        Assert.Contains("-RunId", args);
        Assert.Contains("test-run-id", args);
        Assert.DoesNotContain("-ArticlePath", args);
    }

    [Fact]
    public void BuildArguments_SingleArticlePath_EmitsArticlePath()
    {
        var request = new ValidationScriptRequest(
            ScriptPath: "Test-ArticleHealth.ps1",
            RunId: "run-1",
            Namespace: "storage",
            RepoRoot: "/repo",
            OutputRoot: "/output",
            OutputJsonPath: "/output/validation/article-health.json",
            ArticlePaths: ["/output/tool-family/storage.md"]);

        var args = ValidationScriptRunner.BuildArguments(request);

        Assert.Contains("-ArticlePath", args);
        Assert.Contains("/output/tool-family/storage.md", args);
        Assert.DoesNotContain("-ArticlesDir", args);
    }

    [Fact]
    public void BuildArguments_MultipleArticlePaths_EmitsArticlesDir()
    {
        var request = new ValidationScriptRequest(
            ScriptPath: "Test-ArticleHealth.ps1",
            RunId: "run-1",
            Namespace: "storage",
            RepoRoot: "/repo",
            OutputRoot: "/output",
            OutputJsonPath: "/output/validation/article-health.json",
            ArticlePaths: ["/output/tool-family/storage.md", "/output/tool-family/storage-blob.md"]);

        var args = ValidationScriptRunner.BuildArguments(request);

        Assert.Contains("-ArticlesDir", args);
        Assert.DoesNotContain("-ArticlePath", args);
    }

    [Fact]
    public void BuildArguments_ArticlePathsAndAdditionalArticlesDir_Throws()
    {
        var request = new ValidationScriptRequest(
            ScriptPath: "Test-ArticleHealth.ps1",
            RunId: "run-1",
            Namespace: "storage",
            RepoRoot: "/repo",
            OutputRoot: "/output",
            OutputJsonPath: "/output/validation/article-health.json",
            ArticlePaths: ["/output/tool-family/storage.md"],
            AdditionalArguments: new Dictionary<string, string>
            {
                ["-ArticlesDir"] = "/output/tool-family"
            });

        Assert.Throws<ArgumentException>(() => ValidationScriptRunner.BuildArguments(request));
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
        var root = Path.Combine(Path.GetTempPath(), $"cov-step-test-{Guid.NewGuid():N}");
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

        File.WriteAllText(Path.Combine(root, "mcp-doc-generation.sln"), string.Empty);

        return new TestEnvironment(root, mcpTools, output, gateConfigPath);
    }

    private static void CreateCliOutput(TestEnvironment env)
    {
        var cliDir = Path.Combine(env.OutputPath, "cli");
        Directory.CreateDirectory(cliDir);
        var cliOutput = new { results = new[] { new { command = "storage list" } } };
        File.WriteAllText(
            Path.Combine(cliDir, "cli-output.json"),
            JsonSerializer.Serialize(cliOutput));
    }

    private static void CreateArticlesDirectory(TestEnvironment env)
    {
        var dir = Path.Combine(env.OutputPath, "tool-family");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "storage.md"), "# Storage\n\nContent.");
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
            new PipelineRequest("storage", [8], env.OutputPath,
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
            checks = new[]
            {
                new { name = "tools_coverage", status = verdict == "fail" ? "fail" : "pass", detail = "5/5 tools documented" },
                new { name = "params_coverage", status = verdict == "warn" ? "warn" : "pass", detail = "20/22 params documented" },
                new { name = "annotation_accuracy", status = "pass", detail = "0 mismatches" },
            },
            summary = new
            {
                tools_documented = 5,
                tools_missing = verdict == "fail" ? 2 : 0,
                params_documented = 20,
                params_missing = verdict == "warn" ? 2 : 0,
                annotation_mismatches = 0,
            }
        };
        return JsonSerializer.Serialize(obj);
    }

    // ── Process runner stubs ──────────────────────────────────────────────────

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

    private sealed class SucceedingProcessRunner() : IProcessRunner
    {
        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken ct)
            => ValueTask.FromResult(new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory, 0, "", "", TimeSpan.Zero));

        public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken ct) => RunAsync(new ProcessSpec("dotnet", [], "."), ct);
        public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(string p, IEnumerable<string> a, bool nb, string wd, CancellationToken ct) => RunAsync(new ProcessSpec("dotnet", [], wd), ct);

        public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(string scriptPath, IEnumerable<string> arguments, string workingDirectory, CancellationToken ct)
            => ValueTask.FromResult(new ProcessExecutionResult("pwsh", ["-File", scriptPath], workingDirectory, 0, "", "", TimeSpan.Zero));
    }

    private sealed class ArtifactWritingProcessRunner(string artifactContent) : IProcessRunner
    {
        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken ct)
            => ValueTask.FromResult(new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory, 0, "", "", TimeSpan.Zero));

        public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken ct) => RunAsync(new ProcessSpec("dotnet", [], "."), ct);
        public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(string p, IEnumerable<string> a, bool nb, string wd, CancellationToken ct) => RunAsync(new ProcessSpec("dotnet", [], wd), ct);

        public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(string scriptPath, IEnumerable<string> arguments, string workingDirectory, CancellationToken ct)
        {
            var argList = arguments.ToList();
            var outputJsonPath = ExtractArg(argList, "-OutputJson");
            if (outputJsonPath is not null)
            {
                var dir = Path.GetDirectoryName(outputJsonPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(outputJsonPath, artifactContent);
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
