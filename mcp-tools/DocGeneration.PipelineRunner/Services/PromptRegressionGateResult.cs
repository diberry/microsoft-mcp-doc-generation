namespace PipelineRunner.Services;

/// <summary>
/// Outcome of the prompt regression test suite gate.
/// </summary>
public sealed record PromptRegressionGateResult(bool Success, string Reason)
{
    public static PromptRegressionGateResult Pass(string reason) => new(true, reason);
    public static PromptRegressionGateResult Fail(string reason) => new(false, reason);
}
