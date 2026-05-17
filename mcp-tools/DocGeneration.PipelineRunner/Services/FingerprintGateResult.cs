namespace PipelineRunner.Services;

/// <summary>
/// Outcome of the fingerprint baseline comparison gate.
/// </summary>
public sealed record FingerprintGateResult(bool Success, string Reason)
{
    public static FingerprintGateResult Pass(string reason) => new(true, reason);
    public static FingerprintGateResult Fail(string reason) => new(false, reason);
}
