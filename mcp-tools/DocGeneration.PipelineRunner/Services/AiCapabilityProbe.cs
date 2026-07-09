namespace PipelineRunner.Services;

public sealed class AiCapabilityProbe : IAiCapabilityProbe
{
    private const string ApiKey = "FOUNDRY_API_KEY";
    private const string Endpoint = "FOUNDRY_ENDPOINT";
    private const string ModelName = "FOUNDRY_MODEL_NAME";
    private const string UseDefaultCredential = "FOUNDRY_USE_DEFAULT_CREDENTIAL";

    // Keys read from the process environment so that either auth mode can be detected.
    private static readonly string[] KnownKeys =
    [
        ApiKey,
        Endpoint,
        ModelName,
        UseDefaultCredential,
    ];

    public async ValueTask<AiCapabilityResult> ProbeAsync(string mcpToolsRoot, CancellationToken cancellationToken)
    {
        var envFilePath = Path.Combine(mcpToolsRoot, ".env");
        var envValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in KnownKeys)
        {
            var envValue = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(envValue))
            {
                envValues[key] = envValue;
            }
        }

        if (File.Exists(envFilePath))
        {
            var lines = await File.ReadAllLinesAsync(envFilePath, cancellationToken);
            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                var value = line[(separatorIndex + 1)..].Trim().Trim('"');
                if (!string.IsNullOrWhiteSpace(value))
                {
                    envValues[key] = value;
                }
            }
        }

        var useDefaultCredential = ParseBoolean(
            envValues.TryGetValue(UseDefaultCredential, out var flag) ? flag : null);

        // When default-credential auth is enabled the API key is optional; only the endpoint and
        // model name are required. Otherwise the API key remains mandatory. Mirrors the runtime
        // AI client (GenerativeAIOptions/GenerativeAIClient) so the probe never blocks a valid
        // default-credential configuration.
        var requiredKeys = useDefaultCredential
            ? new[] { Endpoint, ModelName }
            : new[] { ApiKey, Endpoint, ModelName };

        var missing = requiredKeys.Where(key => !envValues.ContainsKey(key)).ToArray();
        return new AiCapabilityResult(missing.Length == 0, missing);
    }

    // Mirrors ParseBoolean in GenerativeAIOptions: truthy = "1"/"true"/"yes"/"on" (case-insensitive).
    private static bool ParseBoolean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }
}
