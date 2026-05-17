namespace PipelineRunner.Services;

public interface IPromptRegressionGate
{
    Task<PromptRegressionGateResult> EvaluateAsync(
        string mcpToolsRoot,
        CancellationToken cancellationToken);
}
