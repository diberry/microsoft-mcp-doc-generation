using System.Text.RegularExpressions;

namespace ExamplePrompts;

public class ExamplePromptsOptions
{
    public string? ApiKey { get; set; }
    public string? Instance { get; set; }
    public string? Endpoint { get; set; }
    public string? Model { get; set; }
    public string? ModelApiVersion { get; set; }

    private const string DotEnvFileName = ".env";

    public static ExamplePromptsOptions LoadFromEnvironmentOrDotEnv(string? basePath = null)
    {
        var opts = new ExamplePromptsOptions();

        opts.ApiKey = Environment.GetEnvironmentVariable("FOUNDRY_API_KEY");
        opts.Instance = Environment.GetEnvironmentVariable("FOUNDRY_INSTANCE");
        opts.Endpoint = Environment.GetEnvironmentVariable("FOUNDRY_ENDPOINT");
        opts.Model = Environment.GetEnvironmentVariable("FOUNDRY_MODEL");
        opts.ModelApiVersion = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_API_VERSION");

        if (string.IsNullOrEmpty(opts.ApiKey) || string.IsNullOrEmpty(opts.Endpoint) || string.IsNullOrEmpty(opts.Model))
        {
            var path = basePath ?? Directory.GetCurrentDirectory();
            var envPath = Path.Combine(path, DotEnvFileName);
            if (File.Exists(envPath))
            {
                var kv = ParseDotEnv(File.ReadAllText(envPath));
                opts.ApiKey ??= TryGet(kv, "FOUNDRY_API_KEY");
                opts.Instance ??= TryGet(kv, "FOUNDRY_INSTANCE");
                opts.Endpoint ??= TryGet(kv, "FOUNDRY_ENDPOINT");
                opts.Model ??= TryGet(kv, "FOUNDRY_MODEL");
                opts.ModelApiVersion ??= TryGet(kv, "FOUNDRY_MODEL_API_VERSION");
            }
        }

        return opts;
    }

    private static string? TryGet(Dictionary<string, string> dict, string key)
        => dict.TryGetValue(key, out var v) ? v : null;

    private static Dictionary<string, string> ParseDotEnv(string content)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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
