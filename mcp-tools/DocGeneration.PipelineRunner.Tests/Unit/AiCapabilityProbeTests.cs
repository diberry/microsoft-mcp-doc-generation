using PipelineRunner.Services;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Tests for the real <see cref="AiCapabilityProbe"/> that reads AI configuration from
/// process environment variables merged with a <c>.env</c> file. These tests write a temp
/// <c>.env</c> file and clear the relevant process environment variables so the probe reads
/// only the file, keeping assertions deterministic across machines and CI.
/// </summary>
public sealed class AiCapabilityProbeTests
{
    private static readonly string[] FoundryEnvKeys =
    [
        "FOUNDRY_API_KEY",
        "FOUNDRY_ENDPOINT",
        "FOUNDRY_MODEL_NAME",
        "FOUNDRY_USE_DEFAULT_CREDENTIAL",
    ];

    [Fact]
    public async Task ProbeAsync_DefaultCredentialEnabledWithoutApiKey_IsConfigured()
    {
        // Regression for the bootstrap failure: default-credential auth omits FOUNDRY_API_KEY,
        // so the probe must NOT require it when FOUNDRY_USE_DEFAULT_CREDENTIAL is truthy.
        using var env = new TempEnvFile(
            "FOUNDRY_USE_DEFAULT_CREDENTIAL=\"true\"",
            "FOUNDRY_ENDPOINT=\"https://oai-contoso-speech.openai.azure.com/\"",
            "FOUNDRY_MODEL_NAME=\"gpt-4o\"",
            "FOUNDRY_INSTANCE=\"oai-contoso-speech\"",
            "KEYVAULT_URI=\"https://kv-contoso-speech.vault.azure.net/\"");

        var result = await Probe(env);

        Assert.True(result.IsConfigured);
        Assert.Empty(result.MissingKeys);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("ON")]
    [InlineData("True")]
    public async Task ProbeAsync_DefaultCredentialTruthyVariants_IsConfigured(string flagValue)
    {
        using var env = new TempEnvFile(
            $"FOUNDRY_USE_DEFAULT_CREDENTIAL=\"{flagValue}\"",
            "FOUNDRY_ENDPOINT=\"https://cosmos-foundry.openai.azure.com/\"",
            "FOUNDRY_MODEL_NAME=\"gpt-4o-mini\"");

        var result = await Probe(env);

        Assert.True(result.IsConfigured);
        Assert.Empty(result.MissingKeys);
    }

    [Fact]
    public async Task ProbeAsync_NoDefaultCredentialAndNoApiKey_MissingApiKey()
    {
        // Guards the original behavior: without default credential, the API key is required.
        using var env = new TempEnvFile(
            "FOUNDRY_ENDPOINT=\"https://kv-monitor-foundry.openai.azure.com/\"",
            "FOUNDRY_MODEL_NAME=\"gpt-4o\"");

        var result = await Probe(env);

        Assert.False(result.IsConfigured);
        Assert.Contains("FOUNDRY_API_KEY", result.MissingKeys);
    }

    [Fact]
    public async Task ProbeAsync_ApiKeyPresentWithoutDefaultCredential_IsConfigured()
    {
        // Happy-path regression: API-key auth continues to work with no default-credential flag.
        using var env = new TempEnvFile(
            "FOUNDRY_API_KEY=\"sk-storage-abc123def456\"",
            "FOUNDRY_ENDPOINT=\"https://storage-foundry.openai.azure.com/\"",
            "FOUNDRY_MODEL_NAME=\"gpt-4o\"");

        var result = await Probe(env);

        Assert.True(result.IsConfigured);
        Assert.Empty(result.MissingKeys);
    }

    [Fact]
    public async Task ProbeAsync_DefaultCredentialFalseAndNoApiKey_MissingApiKey()
    {
        // An explicitly falsy flag must still require the API key.
        using var env = new TempEnvFile(
            "FOUNDRY_USE_DEFAULT_CREDENTIAL=\"false\"",
            "FOUNDRY_ENDPOINT=\"https://aks-foundry.openai.azure.com/\"",
            "FOUNDRY_MODEL_NAME=\"gpt-4o\"");

        var result = await Probe(env);

        Assert.False(result.IsConfigured);
        Assert.Contains("FOUNDRY_API_KEY", result.MissingKeys);
    }

    private static async Task<AiCapabilityResult> Probe(TempEnvFile env)
        => await new AiCapabilityProbe().ProbeAsync(env.McpToolsRoot, CancellationToken.None);

    /// <summary>
    /// Creates an isolated temp directory containing a <c>.env</c> file and clears the FOUNDRY_*
    /// process environment variables for the lifetime of the instance so the probe reads only the
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

            McpToolsRoot = Path.Combine(Path.GetTempPath(), $"ai-capability-probe-tests-{Guid.NewGuid():N}");
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
