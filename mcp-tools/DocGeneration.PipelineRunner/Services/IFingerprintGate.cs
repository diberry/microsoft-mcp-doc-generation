namespace PipelineRunner.Services;

public interface IFingerprintGate
{
    Task<FingerprintGateResult> EvaluateAsync(
        string repoRoot,
        string mcpToolsRoot,
        CancellationToken cancellationToken);
}
