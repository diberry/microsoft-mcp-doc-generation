using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="PromptRegressionGate"/>.
/// Uses a recording process runner to avoid real dotnet test invocations.
/// </summary>
public class PromptRegressionGateTests
{
    // ── Test project not found ─────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_TestProjectNotFound_ReturnsFail()
    {
        var mcpToolsRoot = Path.Combine(Path.GetTempPath(), $"prg-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(mcpToolsRoot);
        try
        {
            // No DocGeneration.PromptRegression.Tests project in mcpToolsRoot
            var gate = BuildGate(out _, out _, exitCode: 0);
            var result = await gate.EvaluateAsync(mcpToolsRoot, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("not found", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(mcpToolsRoot, true); }
    }

    // ── Tests pass (exit 0) ────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_TestsPass_ReturnsPass()
    {
        var mcpToolsRoot = CreateMcpToolsRootWithProject();
        try
        {
            var gate = BuildGate(out _, out _, exitCode: 0,
                stdout: "Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12");
            var result = await gate.EvaluateAsync(mcpToolsRoot, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("passed", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(mcpToolsRoot, true); }
    }

    // ── Tests fail (exit 1) ────────────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_TestsFail_ReturnsFail()
    {
        var mcpToolsRoot = CreateMcpToolsRootWithProject();
        try
        {
            var gate = BuildGate(out _, out _, exitCode: 1,
                stdout: "Failed!  - Failed: 2, Passed: 10, Skipped: 0, Total: 12");
            var result = await gate.EvaluateAsync(mcpToolsRoot, CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("failed", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(mcpToolsRoot, true); }
    }

    // ── Verifies correct dotnet test arguments ─────────────────────────────

    [Fact]
    public async Task EvaluateAsync_InvokesDotNetTest_WithCorrectProjectAndFlags()
    {
        var mcpToolsRoot = CreateMcpToolsRootWithProject();
        try
        {
            var gate = BuildGate(out var runner, out _, exitCode: 0);
            await gate.EvaluateAsync(mcpToolsRoot, CancellationToken.None);

            Assert.Single(runner.Invocations);
            var args = string.Join(" ", runner.Invocations[0].Arguments);
            Assert.Contains("test", args, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("DocGeneration.PromptRegression.Tests", args, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("--no-build", args, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Release", args, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(mcpToolsRoot, true); }
    }

    // ── Test summary line is captured in pass reason ───────────────────────

    [Fact]
    public async Task EvaluateAsync_SummaryLineAppended_ToPassReason()
    {
        var mcpToolsRoot = CreateMcpToolsRootWithProject();
        try
        {
            const string summaryLine = "Passed! - Failed: 0, Passed: 7, Skipped: 0, Total: 7";
            var gate = BuildGate(out _, out _, exitCode: 0, stdout: $"some output\n{summaryLine}");
            var result = await gate.EvaluateAsync(mcpToolsRoot, CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("Total: 7", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(mcpToolsRoot, true); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static PromptRegressionGate BuildGate(
        out FixedProcessRunner runner,
        out BufferedReportWriter writer,
        int exitCode = 0,
        string stdout = "")
    {
        var r = new FixedProcessRunner(exitCode, stdout);
        runner = r;
        writer = new BufferedReportWriter();
        return new PromptRegressionGate(r, writer);
    }

    private static string CreateMcpToolsRootWithProject()
    {
        var mcpToolsRoot = Path.Combine(Path.GetTempPath(), $"prg-test-{Guid.NewGuid():N}");
        var projectDir = Path.Combine(mcpToolsRoot, "DocGeneration.PromptRegression.Tests");
        Directory.CreateDirectory(projectDir);
        File.WriteAllText(
            Path.Combine(projectDir, "DocGeneration.PromptRegression.Tests.csproj"),
            "<Project />");
        return mcpToolsRoot;
    }

    /// <summary>
    /// A lightweight process runner that returns a fixed exit code and stdout for every call.
    /// </summary>
    private sealed class FixedProcessRunner : IProcessRunner
    {
        private readonly int _exitCode;
        private readonly string _stdout;

        public FixedProcessRunner(int exitCode, string stdout)
        {
            _exitCode = exitCode;
            _stdout = stdout;
        }

        public List<ProcessSpec> Invocations { get; } = new();

        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken ct)
        {
            Invocations.Add(spec);
            return ValueTask.FromResult(
                new ProcessExecutionResult(spec.FileName, spec.Arguments, spec.WorkingDirectory,
                    _exitCode, _stdout, string.Empty, TimeSpan.Zero));
        }

        public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken ct)
            => RunAsync(new ProcessSpec("dotnet", ["build", solutionPath], string.Empty), ct);

        public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(string projectPath, IEnumerable<string> arguments, bool noBuild, string workingDirectory, CancellationToken ct)
            => RunAsync(new ProcessSpec("dotnet", ["run", "--project", projectPath, .. arguments], workingDirectory), ct);

        public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(string scriptPath, IEnumerable<string> arguments, string workingDirectory, CancellationToken ct)
            => RunAsync(new ProcessSpec("pwsh", ["-File", scriptPath, .. arguments], workingDirectory), ct);
    }
}
