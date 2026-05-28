using System.Diagnostics;

namespace DocGeneration.McpCliMetadata;

internal interface IProcessRunner
{
    Task<ProcessRunResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
}

internal record ProcessRunResult(int ExitCode, string Output, string Error);

internal sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessRunResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        // Read both streams concurrently to prevent deadlocks
        await Task.WhenAll(outputTask, errorTask);
        await process.WaitForExitAsync(cancellationToken);

        return new ProcessRunResult(process.ExitCode, await outputTask, await errorTask);
    }
}

internal sealed class AzmcpRunner
{
    private const string Binary = "azmcp";
    private const int DefaultTimeoutMs = 30_000;
    private readonly IProcessRunner _runner;

    internal AzmcpRunner(IProcessRunner? runner = null)
    {
        _runner = runner ?? new ProcessRunner();
    }

    internal async Task<string> GetVersionAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(DefaultTimeoutMs);
        var result = await _runner.RunAsync(Binary, "--version", cts.Token);
        ThrowIfFailed(result, "--version");
        var trimmed = result.Output.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new InvalidOperationException("azmcp --version returned empty output");
        return trimmed;
    }

    internal async Task<string> GetToolsJsonAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(DefaultTimeoutMs);
        var result = await _runner.RunAsync(Binary, "tools list", cts.Token);
        ThrowIfFailed(result, "tools list");
        return result.Output.Trim();
    }

    internal async Task<string> GetNamespaceJsonAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(DefaultTimeoutMs);
        var result = await _runner.RunAsync(Binary, "tools list --namespace-mode", cts.Token);
        ThrowIfFailed(result, "tools list --namespace-mode");
        return result.Output.Trim();
    }

    private static void ThrowIfFailed(ProcessRunResult result, string command)
    {
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"azmcp {command} failed with exit code {result.ExitCode}: {result.Error.Trim()}");
        }
    }
}
