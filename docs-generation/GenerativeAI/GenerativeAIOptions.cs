using System.Text.RegularExpressions;
using Shared;

namespace GenerativeAI;

public class GenerativeAIOptions
{
    public string? ApiKey { get; set; }
    public string? Endpoint { get; set; }
    public string? Deployment { get; set; }
    public string? ApiVersion { get; set; }

    private const string DotEnvFileName = ".env";

    public static GenerativeAIOptions LoadFromEnvironmentOrDotEnv(string? basePath = null)
    {
        // Log verbose debug information to file
        LogFileHelper.WriteDebug("=== C# Environment Variable Loading ===");
        LogFileHelper.WriteDebug("Current Directory: " + Directory.GetCurrentDirectory());
        LogFileHelper.WriteDebug("AppContext.BaseDirectory: " + AppContext.BaseDirectory);
        LogFileHelper.WriteDebug("");
        
        var opts = new GenerativeAIOptions();
        opts.ApiKey = Environment.GetEnvironmentVariable("FOUNDRY_API_KEY");
        opts.Endpoint = Environment.GetEnvironmentVariable("FOUNDRY_ENDPOINT");
        opts.Deployment = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_NAME") ?? Environment.GetEnvironmentVariable("FOUNDRY_MODEL") ?? Environment.GetEnvironmentVariable("FOUNDRY_INSTANCE");
        opts.ApiVersion = Environment.GetEnvironmentVariable("FOUNDRY_MODEL_API_VERSION");
        
        LogFileHelper.WriteDebug("Environment variables read from process:");
        LogFileHelper.WriteDebug($"  FOUNDRY_API_KEY: {(string.IsNullOrEmpty(opts.ApiKey) ? "NOT SET" : $"SET ({opts.ApiKey.Length} chars) - {opts.ApiKey.Substring(0, Math.Min(20, opts.ApiKey.Length))}...")}");
        LogFileHelper.WriteDebug($"  FOUNDRY_ENDPOINT: {opts.Endpoint ?? "NOT SET"}");
        LogFileHelper.WriteDebug($"  FOUNDRY_MODEL_NAME: {opts.Deployment ?? "NOT SET"}");
        LogFileHelper.WriteDebug($"  FOUNDRY_MODEL_API_VERSION: {opts.ApiVersion ?? "NOT SET"}");
        LogFileHelper.WriteDebug("");

        if (string.IsNullOrEmpty(opts.ApiKey) || string.IsNullOrEmpty(opts.Endpoint) || string.IsNullOrEmpty(opts.Deployment))
        {
            // Look for .env in docs-generation directory
            // Try multiple paths: from bin/Release/net9.0, from project dir, from working dir
            var searchPaths = new[]
            {
                basePath ?? Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", ".."), // From bin/Release/net9.0
                basePath ?? Path.Combine(Directory.GetCurrentDirectory(), ".."),                   // From CSharpGenerator to docs-generation
                basePath ?? Directory.GetCurrentDirectory(),                                       // From docs-generation
                basePath ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..")        // From bin/Release/net9.0 via AppContext
            };

            LogFileHelper.WriteDebug("Attempting to load from .env file (fallback)...");
            foreach (var path in searchPaths)
            {
                var envPath = Path.GetFullPath(Path.Combine(path, DotEnvFileName));
                LogFileHelper.WriteDebug($"  Checking: {envPath}");
                if (File.Exists(envPath))
                {
                    LogFileHelper.WriteDebug($"  ✅ Found .env file at: {envPath}");
                    var content = File.ReadAllText(envPath);
                    LogFileHelper.WriteDebug($"  📄 .env file contents ({content.Length} chars):");
                    LogFileHelper.WriteDebug(content);
                    LogFileHelper.WriteDebug("");
                    
                    var kv = ParseDotEnv(content);
                    LogFileHelper.WriteDebug($"  Parsed {kv.Count} variables from .env:");
                    foreach (var kvp in kv)
                    {
                        var displayValue = kvp.Key.Contains("KEY") || kvp.Key.Contains("SECRET") 
                            ? $"{kvp.Value.Substring(0, Math.Min(20, kvp.Value.Length))}..." 
                            : kvp.Value;
                        LogFileHelper.WriteDebug($"    {kvp.Key} = {displayValue}");
                    }
                    LogFileHelper.WriteDebug("");
                    
                    opts.ApiKey ??= TryGet(kv, "FOUNDRY_API_KEY");
                    opts.Endpoint ??= TryGet(kv, "FOUNDRY_ENDPOINT");
                    opts.Deployment ??= TryGet(kv, "FOUNDRY_MODEL_NAME") ?? TryGet(kv, "FOUNDRY_MODEL") ?? TryGet(kv, "FOUNDRY_INSTANCE");
                    opts.ApiVersion ??= TryGet(kv, "FOUNDRY_MODEL_API_VERSION");
                    
                    LogFileHelper.WriteDebug("  Final values after .env fallback:");
                    LogFileHelper.WriteDebug($"    FOUNDRY_API_KEY: {(string.IsNullOrEmpty(opts.ApiKey) ? "NOT SET" : $"SET ({opts.ApiKey.Length} chars)")}");
                    LogFileHelper.WriteDebug($"    FOUNDRY_ENDPOINT: {opts.Endpoint ?? "NOT SET"}");
                    LogFileHelper.WriteDebug($"    FOUNDRY_MODEL_NAME: {opts.Deployment ?? "NOT SET"}");
                    LogFileHelper.WriteDebug($"    FOUNDRY_MODEL_API_VERSION: {opts.ApiVersion ?? "NOT SET"}");
                    LogFileHelper.WriteDebug("");
                    break; // Found the file, stop searching
                }
            }
        }
        
        // Minimal console output - just success indicator
        var hasAllRequired = !string.IsNullOrEmpty(opts.ApiKey) && !string.IsNullOrEmpty(opts.Endpoint) && !string.IsNullOrEmpty(opts.Deployment);
        if (hasAllRequired)
        {
            Console.WriteLine("✓ Azure OpenAI credentials loaded");
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
