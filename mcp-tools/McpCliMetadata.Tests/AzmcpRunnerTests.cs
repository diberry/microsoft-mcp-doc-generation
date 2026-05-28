using DocGeneration.McpCliMetadata;
using Xunit;

namespace DocGeneration.McpCliMetadata.Tests;

public class AzmcpRunnerTests
{
    [Fact]
    public async Task GetVersionAsync_ReturnsVersion()
    {
        var runner = new AzmcpRunner(new FakeProcessRunner("3.0.0-beta.12+abc123", 0));
        var version = await runner.GetVersionAsync();
        Assert.Equal("3.0.0-beta.12+abc123", version);
    }

    [Fact]
    public async Task GetVersionAsync_TrimsWhitespace()
    {
        var runner = new AzmcpRunner(new FakeProcessRunner("  3.0.0-beta.12  \n", 0));
        var version = await runner.GetVersionAsync();
        Assert.Equal("3.0.0-beta.12", version);
    }

    [Fact]
    public async Task GetVersionAsync_ThrowsOnNonZeroExit()
    {
        var runner = new AzmcpRunner(new FakeProcessRunner("", 1, "azmcp not found"));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.GetVersionAsync());
        Assert.Contains("exit code 1", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetToolsJsonAsync_ReturnsJson()
    {
        const string json = """{"results": [{"command": "azure storage blob list", "name": "azure-storage-blob-list"}]}""";
        var runner = new AzmcpRunner(new FakeProcessRunner(json, 0));
        var result = await runner.GetToolsJsonAsync();
        Assert.Equal(json, result);
    }

    [Fact]
    public async Task GetToolsJsonAsync_ThrowsOnNonZeroExit()
    {
        var runner = new AzmcpRunner(new FakeProcessRunner("", 1, "tools list error"));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.GetToolsJsonAsync());
        Assert.Contains("tools list", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetNamespaceJsonAsync_ReturnsJson()
    {
        const string json = """{"results": [{"name": "storage"}, {"name": "keyvault"}]}""";
        var runner = new AzmcpRunner(new FakeProcessRunner(json, 0));
        var result = await runner.GetNamespaceJsonAsync();
        Assert.Equal(json, result);
    }

    [Fact]
    public async Task GetNamespaceJsonAsync_ThrowsOnNonZeroExit()
    {
        var runner = new AzmcpRunner(new FakeProcessRunner("", 1, "namespace mode error"));
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => runner.GetNamespaceJsonAsync());
        Assert.Contains("namespace-mode", ex.Message, StringComparison.Ordinal);
    }
}

internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly string _output;
    private readonly int _exitCode;
    private readonly string _error;

    internal FakeProcessRunner(string output, int exitCode, string error = "")
    {
        _output = output;
        _exitCode = exitCode;
        _error = error;
    }

    public Task<ProcessRunResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
        => Task.FromResult(new ProcessRunResult(_exitCode, _output, _error));
}
