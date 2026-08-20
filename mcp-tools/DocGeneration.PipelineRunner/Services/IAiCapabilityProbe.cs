namespace PipelineRunner.Services;

public sealed record AiCapabilityResult(bool IsConfigured, IReadOnlyList<string> MissingKeys);

/// <summary>
/// Result of a live Azure OpenAI endpoint health check: <see cref="Success"/> is true only when
/// the endpoint actually responded to a real request.
/// </summary>
public sealed record AiEndpointHealthCheckResult(bool Success, string? ErrorMessage);

public interface IAiCapabilityProbe
{
    ValueTask<AiCapabilityResult> ProbeAsync(string mcpToolsRoot, CancellationToken cancellationToken);

    /// <summary>
    /// Makes a very early, live call against the configured Azure OpenAI endpoint to prove it
    /// actually works — as opposed to <see cref="ProbeAsync"/>, which only checks that
    /// configuration values (endpoint/key/deployment) are present. Called once at Bootstrap,
    /// before any AI-dependent namespace step, so a broken endpoint (wrong URL, revoked key,
    /// model not deployed, network/firewall issue) is caught immediately instead of failing
    /// namespace-by-namespace across Steps 2, 3, 4, and 6.
    /// </summary>
    Task<AiEndpointHealthCheckResult> LiveCheckAsync(string mcpToolsRoot, CancellationToken cancellationToken);
}
