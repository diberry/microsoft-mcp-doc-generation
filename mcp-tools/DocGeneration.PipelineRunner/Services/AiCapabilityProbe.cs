namespace PipelineRunner.Services;

public sealed class AiCapabilityProbe : IAiCapabilityProbe
{
    private static readonly string[] RequiredKeys =
    [
        "FOUNDRY_API_KEY",
        "FOUNDRY_ENDPOINT",
        "FOUNDRY_MODEL_NAME",
    ];

    public async ValueTask<AiCapabilityResult> ProbeAsync(string docsGenerationRoot, CancellationToken cancellationToken)
    {
        var envFilePath = Path.Combine(docsGenerationRoot, ".env");
        var envValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in RequiredKeys)
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

        var missing = RequiredKeys.Where(key => !envValues.ContainsKey(key)).ToArray();
        return new AiCapabilityResult(missing.Length == 0, missing);
    }
}
