namespace PipelineRunner.Services;

/// <summary>
/// Compares current pipeline output fingerprints against the committed
/// <c>fingerprint-baseline.json</c> to detect unintended output drift.
///
/// <para>Invokes the <c>DocGeneration.Tools.Fingerprint</c> project as a subprocess
/// (snapshot phase then diff phase) to avoid a hard project reference between two
/// Exe outputs.</para>
/// </summary>
public sealed class FingerprintGate : IFingerprintGate
{
    internal const string BaselineFileName = "fingerprint-baseline.json";
    internal const string CandidateFileName = "fingerprint-candidate.json";
    internal const string FingerprintProjectRelPath =
        "DocGeneration.Tools.Fingerprint/DocGeneration.Tools.Fingerprint.csproj";

    private readonly IProcessRunner _processRunner;
    private readonly IReportWriter _reportWriter;

    public FingerprintGate(IProcessRunner processRunner, IReportWriter reportWriter)
    {
        _processRunner = processRunner;
        _reportWriter = reportWriter;
    }

    /// <inheritdoc />
    public async Task<FingerprintGateResult> EvaluateAsync(
        string repoRoot,
        string mcpToolsRoot,
        CancellationToken cancellationToken)
    {
        var baselinePath = Path.Combine(repoRoot, BaselineFileName);
        if (!File.Exists(baselinePath))
        {
            return FingerprintGateResult.Pass(
                $"Fingerprint baseline not found at '{baselinePath}'; skipping comparison as safe fallback.");
        }

        var candidatePath = Path.Combine(repoRoot, CandidateFileName);
        var fingerprintProject = Path.Combine(mcpToolsRoot, FingerprintProjectRelPath);

        try
        {
            // Phase 1: generate candidate snapshot of current generated-* output
            _reportWriter.Info("  Fingerprint gate: generating candidate snapshot...");
            var snapshotResult = await _processRunner.RunAsync(
                new ProcessSpec(
                    "dotnet",
                    [
                        "run", "--project", fingerprintProject,
                        "--configuration", "Release", "--no-build", "--",
                        "snapshot", "--output", candidatePath, "--repo-root", repoRoot
                    ],
                    repoRoot),
                cancellationToken);

            if (!snapshotResult.Succeeded)
            {
                return FingerprintGateResult.Fail(
                    $"Fingerprint snapshot failed (exit {snapshotResult.ExitCode}): {snapshotResult.StandardError}");
            }

            if (!File.Exists(candidatePath))
            {
                return FingerprintGateResult.Fail(
                    "Fingerprint snapshot produced no output file.");
            }

            // Phase 2: diff candidate against baseline
            _reportWriter.Info("  Fingerprint gate: comparing against baseline...");
            var diffResult = await _processRunner.RunAsync(
                new ProcessSpec(
                    "dotnet",
                    [
                        "run", "--project", fingerprintProject,
                        "--configuration", "Release", "--no-build", "--",
                        "diff", "--baseline", baselinePath, "--candidate", candidatePath
                    ],
                    repoRoot),
                cancellationToken);

            if (diffResult.Succeeded)
            {
                return FingerprintGateResult.Pass(
                    "No quality regressions detected in fingerprint comparison.");
            }

            // Trim output to a reasonable summary size
            var output = diffResult.StandardOutput;
            var summary = output.Length > 500 ? output[..500] + "..." : output;
            return FingerprintGateResult.Fail(
                $"Fingerprint comparison detected quality regressions:\n{summary}");
        }
        finally
        {
            // Best-effort cleanup of temporary candidate snapshot
            if (File.Exists(candidatePath))
            {
                try { File.Delete(candidatePath); }
                catch { /* ignored */ }
            }
        }
    }
}
