namespace PipelineRunner.Services;

public sealed record AiCapabilityResult(bool IsConfigured, IReadOnlyList<string> MissingKeys);

public interface IAiCapabilityProbe
{
    ValueTask<AiCapabilityResult> ProbeAsync(string docsGenerationRoot, CancellationToken cancellationToken);
}
