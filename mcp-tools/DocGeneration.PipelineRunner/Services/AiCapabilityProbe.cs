namespace PipelineRunner.Services;

using GenerativeAI;

public sealed class AiCapabilityProbe : IAiCapabilityProbe
{
    private const string ApiKey = "FOUNDRY_API_KEY";
    private const string Endpoint = "FOUNDRY_ENDPOINT";
    private const string ModelName = "FOUNDRY_MODEL_NAME";

    public ValueTask<AiCapabilityResult> ProbeAsync(string mcpToolsRoot, CancellationToken cancellationToken)
    {
        // Delegate .env parsing and process-env-vs-.env precedence to the shared loader so the
        // probe stays byte-for-byte consistent with the runtime AI client
        // (GenerativeAIOptions/GenerativeAIClient). The loader strips both single and double
        // quotes and treats the process environment as authoritative. Previously the probe
        // duplicated a simpler parser that stripped only double quotes and let .env override
        // process env, which disagreed with the loader for single-quoted values and for
        // conflicting variables.
        var options = GenerativeAIOptions.LoadFromEnvironmentOrDotEnv(mcpToolsRoot);

        // When default-credential auth is enabled the API key is optional; only the endpoint and
        // model name are required. Otherwise the API key remains mandatory.
        var missing = new List<string>();
        if (!options.UseDefaultCredential && string.IsNullOrWhiteSpace(options.ApiKey))
        {
            missing.Add(ApiKey);
        }

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            missing.Add(Endpoint);
        }

        if (string.IsNullOrWhiteSpace(options.Deployment))
        {
            missing.Add(ModelName);
        }

        return ValueTask.FromResult(new AiCapabilityResult(missing.Count == 0, missing));
    }

    /// <summary>
    /// Makes one small, real chat-completion call against the configured endpoint to prove it
    /// responds. Uses the same <see cref="GenerativeAIOptions"/> load path as
    /// <see cref="ProbeAsync"/> (process env overrides <c>.env</c>), so it stays consistent with
    /// the runtime AI client.
    /// </summary>
    public async Task<AiEndpointHealthCheckResult> LiveCheckAsync(string mcpToolsRoot, CancellationToken cancellationToken)
    {
        var options = GenerativeAIOptions.LoadFromEnvironmentOrDotEnv(mcpToolsRoot);
        var client = new GenerativeAIClient(options);
        return await LiveCheckAsync(client, cancellationToken);
    }

    internal static async Task<AiEndpointHealthCheckResult> LiveCheckAsync(
        GenerativeAIClient client,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await client.GetChatCompletionAsync(
                "You are a non-user-facing health-check probe used once at pipeline startup. Reply with exactly one word.",
                "Reply with the single word: ok",
                ct: cancellationToken,
                toolOrNamespace: "bootstrap-live-probe",
                operation: "AiEndpointHealthCheck");

            return string.IsNullOrWhiteSpace(response)
                ? new AiEndpointHealthCheckResult(false, "The Azure OpenAI endpoint returned an empty response to the live probe call.")
                : new AiEndpointHealthCheckResult(true, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new AiEndpointHealthCheckResult(false, ex.Message);
        }
    }
}
