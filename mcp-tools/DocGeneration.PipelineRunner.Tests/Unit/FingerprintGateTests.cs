using PipelineRunner.Services;
using PipelineRunner.Tests.Fixtures;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Unit tests for <see cref="FingerprintGate"/>.
/// Uses a callback process runner so no real dotnet process is spawned.
/// </summary>
public class FingerprintGateTests
{
    // ── Baseline not found (safe fallback) ────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_BaselineNotFound_PassesWithFallbackReason()
    {
        var repoRoot = CreateTempDir();
        try
        {
            // No fingerprint-baseline.json in repoRoot
            var gate = BuildGate(out var runner, out _);
            var result = await gate.EvaluateAsync(repoRoot, Path.Combine(repoRoot, "mcp-tools"), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("safe fallback", result.Reason, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(runner.Invocations); // no subprocess calls when baseline absent
        }
        finally { Directory.Delete(repoRoot, true); }
    }

    // ── Snapshot subprocess fails ──────────────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_SnapshotFails_ReturnsFail()
    {
        var repoRoot = CreateTempDirWithBaseline();
        try
        {
            var gate = BuildGate(out var runner, out _,
                snapshotExitCode: 1, snapshotStderr: "disk full");
            var result = await gate.EvaluateAsync(repoRoot, Path.Combine(repoRoot, "mcp-tools"), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("snapshot failed", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(repoRoot, true); }
    }

    // ── Snapshot succeeds, diff returns 0 (no regressions) ────────────────

    [Fact]
    public async Task EvaluateAsync_DiffSucceeds_ReturnsPass()
    {
        var repoRoot = CreateTempDirWithBaseline();
        try
        {
            var gate = BuildGate(out var runner, out _,
                snapshotExitCode: 0, diffExitCode: 0,
                createCandidateFile: true);
            var result = await gate.EvaluateAsync(repoRoot, Path.Combine(repoRoot, "mcp-tools"), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Contains("No quality regressions", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(repoRoot, true); }
    }

    // ── Diff exits 1 (quality regressions detected) ───────────────────────

    [Fact]
    public async Task EvaluateAsync_DiffDetectsRegressions_ReturnsFail()
    {
        var repoRoot = CreateTempDirWithBaseline();
        try
        {
            var gate = BuildGate(out var runner, out _,
                snapshotExitCode: 0, diffExitCode: 1,
                diffStdout: "⚠️  1 namespace(s) with quality regressions.",
                createCandidateFile: true);
            var result = await gate.EvaluateAsync(repoRoot, Path.Combine(repoRoot, "mcp-tools"), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("regressions", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(repoRoot, true); }
    }

    // ── Subprocess invocation order: snapshot → diff ───────────────────────

    [Fact]
    public async Task EvaluateAsync_InvokesSnapshotBeforeDiff()
    {
        var repoRoot = CreateTempDirWithBaseline();
        try
        {
            var gate = BuildGate(out var runner, out _,
                snapshotExitCode: 0, diffExitCode: 0,
                createCandidateFile: true);
            await gate.EvaluateAsync(repoRoot, Path.Combine(repoRoot, "mcp-tools"), CancellationToken.None);

            Assert.Equal(2, runner.Invocations.Count);
            Assert.Contains("snapshot", string.Join(" ", runner.Invocations[0].Arguments), StringComparison.OrdinalIgnoreCase);
            Assert.Contains("diff", string.Join(" ", runner.Invocations[1].Arguments), StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(repoRoot, true); }
    }

    // ── Snapshot produces no output file ──────────────────────────────────

    [Fact]
    public async Task EvaluateAsync_SnapshotProducesNoFile_ReturnsFail()
    {
        var repoRoot = CreateTempDirWithBaseline();
        try
        {
            // Snapshot exits 0 but never writes the candidate file
            var gate = BuildGate(out var runner, out _,
                snapshotExitCode: 0, createCandidateFile: false);
            var result = await gate.EvaluateAsync(repoRoot, Path.Combine(repoRoot, "mcp-tools"), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Contains("no output file", result.Reason, StringComparison.OrdinalIgnoreCase);
        }
        finally { Directory.Delete(repoRoot, true); }
    }

    // ── Candidate file is cleaned up after evaluation ─────────────────────

    [Fact]
    public async Task EvaluateAsync_CandidateFileCleanedUpAfterDiff()
    {
        var repoRoot = CreateTempDirWithBaseline();
        try
        {
            var gate = BuildGate(out var runner, out _,
                snapshotExitCode: 0, diffExitCode: 0,
                createCandidateFile: true);
            await gate.EvaluateAsync(repoRoot, Path.Combine(repoRoot, "mcp-tools"), CancellationToken.None);

            var candidatePath = Path.Combine(repoRoot, FingerprintGate.CandidateFileName);
            Assert.False(File.Exists(candidatePath), "Candidate file should be deleted after gate evaluation.");
        }
        finally { Directory.Delete(repoRoot, true); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static FingerprintGate BuildGate(
        out InvocationCapturingRunner runner,
        out BufferedReportWriter writer,
        int snapshotExitCode = 0,
        string snapshotStderr = "",
        int diffExitCode = 0,
        string diffStdout = "",
        bool createCandidateFile = false)
    {
        var capturedRunner = new InvocationCapturingRunner(snapshotExitCode, snapshotStderr, diffExitCode, diffStdout, createCandidateFile);
        runner = capturedRunner;
        writer = new BufferedReportWriter();
        return new FingerprintGate(capturedRunner, writer);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"fp-gate-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string CreateTempDirWithBaseline()
    {
        var dir = CreateTempDir();
        File.WriteAllText(Path.Combine(dir, FingerprintGate.BaselineFileName), "{}");
        return dir;
    }

    /// <summary>
    /// Process runner that records invocations and simulates fingerprint tool behaviour.
    /// First call = snapshot, second call = diff.
    /// </summary>
    private sealed class InvocationCapturingRunner : IProcessRunner
    {
        private readonly int _snapshotExitCode;
        private readonly string _snapshotStderr;
        private readonly int _diffExitCode;
        private readonly string _diffStdout;
        private readonly bool _createCandidateFile;
        private int _callCount;

        public InvocationCapturingRunner(
            int snapshotExitCode, string snapshotStderr,
            int diffExitCode, string diffStdout,
            bool createCandidateFile)
        {
            _snapshotExitCode = snapshotExitCode;
            _snapshotStderr = snapshotStderr;
            _diffExitCode = diffExitCode;
            _diffStdout = diffStdout;
            _createCandidateFile = createCandidateFile;
        }

        public List<ProcessSpec> Invocations { get; } = new();

        public ValueTask<ProcessExecutionResult> RunAsync(ProcessSpec spec, CancellationToken ct)
        {
            Invocations.Add(spec);
            _callCount++;

            // First call is the snapshot, second is the diff
            if (_callCount == 1)
            {
                if (_createCandidateFile)
                {
                    // Simulate fingerprint tool writing candidate file to working dir
                    var candidatePath = spec.Arguments
                        .SkipWhile(a => a != "--output")
                        .Skip(1)
                        .FirstOrDefault();
                    if (candidatePath is not null)
                        File.WriteAllText(candidatePath, "{}");
                }
                return ValueTask.FromResult(Make(spec, _snapshotExitCode, string.Empty, _snapshotStderr));
            }

            return ValueTask.FromResult(Make(spec, _diffExitCode, _diffStdout, string.Empty));
        }

        public ValueTask<ProcessExecutionResult> RunDotNetBuildAsync(string solutionPath, CancellationToken ct)
            => RunAsync(new ProcessSpec("dotnet", ["build", solutionPath], string.Empty), ct);

        public ValueTask<ProcessExecutionResult> RunDotNetProjectAsync(string projectPath, IEnumerable<string> arguments, bool noBuild, string workingDirectory, CancellationToken ct)
            => RunAsync(new ProcessSpec("dotnet", ["run", "--project", projectPath, .. arguments], workingDirectory), ct);

        public ValueTask<ProcessExecutionResult> RunPowerShellScriptAsync(string scriptPath, IEnumerable<string> arguments, string workingDirectory, CancellationToken ct)
            => RunAsync(new ProcessSpec("pwsh", ["-File", scriptPath, .. arguments], workingDirectory), ct);

        private static ProcessExecutionResult Make(ProcessSpec spec, int exitCode, string stdout, string stderr)
            => new(spec.FileName, spec.Arguments, spec.WorkingDirectory, exitCode, stdout, stderr, TimeSpan.Zero);
    }
}
