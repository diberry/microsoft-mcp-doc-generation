using System.Text.RegularExpressions;

namespace AzureOpenAIClient;

public class AzureOpenAIOptions
{
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public string? Deployment { get; set; }
    public string? ApiVersion { get; set; }

    private const string DotEnvFileName = ".env";

    public static AzureOpenAIOptions LoadFromEnvironmentOrDotEnv(string? basePath = null)
    {
        var opts = new AzureOpenAIOptions();
        opts.ApiKey = Environment.GetEnvironmentVariable("FOUNDRY_API_KEY");
        opts.Endpoint = Environment.GetEnvironmentVariable("FOUNDRY_ENDPOINT");
        opts.Deployment = Environment.GetEnvironmentVariable("FOUNDRY_INSTANCE") ?? Environment.GetEnvironmentVariable("FOUNDRY_MODEL");
        opts.ApiVersion = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_API_VERSION");

        if (string.IsNullOrEmpty(opts.ApiKey) || string.IsNullOrEmpty(opts.Endpoint) || string.IsNullOrEmpty(opts.Deployment))
        {
            var path = basePath ?? Directory.GetCurrentDirectory();
            var envPath = Path.Combine(path, DotEnvFileName);
            if (File.Exists(envPath))
            {
                var kv = ParseDotEnv(File.ReadAllText(envPath));
                opts.ApiKey ??= TryGet(kv, "FOUNDRY_API_KEY");
                opts.Endpoint ??= TryGet(kv, "FOUNDRY_ENDPOINT");
                opts.Deployment ??= TryGet(kv, "FOUNDRY_INSTANCE") ?? TryGet(kv, "FOUNDRY_MODEL");
                opts.ApiVersion ??= TryGet(kv, "FOUNDRY_MODEL_API_VERSION");
            }
        }
        return opts;
    }

    private static string? TryGet(Dictionary<string,string> d, string k) => d.TryGetValue(k, out var v) ? v : null;

    private static Dictionary<string,string> ParseDotEnv(string content)
    {
        var dict = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var rx = new Regex(@"^\s*([A-Za-z0-9_]+)\s*=\s*(.*)\s*$");
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("#")) continue;
            var m = rx.Match(line);
            if (!m.Success) continue;
            var key = m.Groups[1].Value;
            var value = m.Groups[2].Value.Trim();
            if ((value.StartsWith("\"") && value.EndsWith("\"")) || (value.StartsWith("'") && value.EndsWith("'")))
            {
                value = value.Substring(1, value.Length - 2);
            }
            dict[key] = value;
        }
        return dict;
    }
}
