using GenerativeAI;
using PipelineRunner.Steps;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Regression tests for the keyless (DefaultAzureCredential) AI configuration used by the
/// tool-family reducer (Step 4) and horizontal article generation (Step 6). Both steps must
/// resolve <see cref="GenerativeAIOptions"/> anchored to the mcp-tools root so the endpoint,
/// deployment, and default-credential flag are loaded from <c>.env</c> regardless of the current
/// working directory. Previously these paths used empty/CWD-relative options, which left the
/// endpoint unset and broke the keyless path with "Azure OpenAI configuration incomplete".
///
/// The tests write a temp <c>.env</c> and clear the FOUNDRY_* process environment variables so
/// resolution reads only the file, keeping assertions deterministic across machines and CI.
/// </summary>
[Trait("Category", "Keyless")]
public sealed class ReducerAiOptionsTests
{
    private static readonly string[] FoundryEnvKeys =
    [
        "FOUNDRY_API_KEY",
        "FOUNDRY_ENDPOINT",
        "FOUNDRY_MODEL_NAME",
        "FOUNDRY_MODEL",
        "FOUNDRY_INSTANCE",
        "FOUNDRY_USE_DEFAULT_CREDENTIAL",
    ];

    private const string KeylessEndpoint = "https://oai-reducer-keyless.cognitiveservices.azure.com/";
    private const string KeylessDeployment = "gpt-5-mini";

    [Fact]
    public void ToolFamilyCleanup_ResolvesKeylessOptionsFromMcpToolsRoot()
    {
        using var env = new TempEnvFile(
            "FOUNDRY_USE_DEFAULT_CREDENTIAL=\"true\"",
            $"FOUNDRY_ENDPOINT=\"{KeylessEndpoint}\"",
            $"FOUNDRY_MODEL_NAME=\"{KeylessDeployment}\"",
            "FOUNDRY_INSTANCE=\"oai-reducer-keyless\"");

        var options = ToolFamilyCleanupStep.ResolveGenerativeAIOptions(env.McpToolsRoot);

        Assert.Equal(KeylessEndpoint, options.Endpoint);
        Assert.Equal(KeylessDeployment, options.Deployment);
        Assert.True(options.UseDefaultCredential);
        Assert.True(string.IsNullOrEmpty(options.ApiKey));
    }

    [Fact]
    public void HorizontalArticles_ResolvesKeylessOptionsFromMcpToolsRoot()
    {
        using var env = new TempEnvFile(
            "FOUNDRY_USE_DEFAULT_CREDENTIAL=\"true\"",
            $"FOUNDRY_ENDPOINT=\"{KeylessEndpoint}\"",
            $"FOUNDRY_MODEL_NAME=\"{KeylessDeployment}\"",
            "FOUNDRY_INSTANCE=\"oai-reducer-keyless\"");

        var options = HorizontalArticlesStep.ResolveGenerativeAIOptions(env.McpToolsRoot);

        Assert.Equal(KeylessEndpoint, options.Endpoint);
        Assert.Equal(KeylessDeployment, options.Deployment);
        Assert.True(options.UseDefaultCredential);
        Assert.True(string.IsNullOrEmpty(options.ApiKey));
    }

    /// <summary>
    /// Creates an isolated temp directory containing a <c>.env</c> file and clears the FOUNDRY_*
    /// process environment variables for the lifetime of the instance so resolution reads only the
    /// file. Restores the previous process values on dispose.
    /// </summary>
    private sealed class TempEnvFile : IDisposable
    {
        private readonly Dictionary<string, string?> _originalValues = new();

        public TempEnvFile(params string[] envLines)
        {
            foreach (var key in FoundryEnvKeys)
            {
                _originalValues[key] = Environment.GetEnvironmentVariable(key);
                Environment.SetEnvironmentVariable(key, null);
            }

            McpToolsRoot = Path.Combine(Path.GetTempPath(), $"reducer-ai-options-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(McpToolsRoot);
            File.WriteAllLines(Path.Combine(McpToolsRoot, ".env"), envLines);
        }

        public string McpToolsRoot { get; }

        public void Dispose()
        {
            foreach (var (key, value) in _originalValues)
            {
                Environment.SetEnvironmentVariable(key, value);
            }

            if (Directory.Exists(McpToolsRoot))
            {
                Directory.Delete(McpToolsRoot, recursive: true);
            }
        }
    }
}
