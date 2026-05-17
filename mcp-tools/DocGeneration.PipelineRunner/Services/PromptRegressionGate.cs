namespace PipelineRunner.Services;

/// <summary>
/// Runs the <c>DocGeneration.PromptRegression.Tests</c> xUnit suite as a pipeline
/// validation gate to ensure that prompt templates still produce expected output
/// after pipeline code changes (e.g., new flags, new gate logic).
/// </summary>
public sealed class PromptRegressionGate : IPromptRegressionGate
{
    internal const string RegressionTestsRelPath =
        "DocGeneration.PromptRegression.Tests/DocGeneration.PromptRegression.Tests.csproj";

    private readonly IProcessRunner _processRunner;
    private readonly IReportWriter _reportWriter;

    public PromptRegressionGate(IProcessRunner processRunner, IReportWriter reportWriter)
    {
        _processRunner = processRunner;
        _reportWriter = reportWriter;
    }

    /// <inheritdoc />
    public async Task<PromptRegressionGateResult> EvaluateAsync(
        string mcpToolsRoot,
        CancellationToken cancellationToken)
    {
        var testProject = Path.Combine(mcpToolsRoot, RegressionTestsRelPath);
        if (!File.Exists(testProject))
        {
            return PromptRegressionGateResult.Fail(
                $"Prompt regression test project not found at '{testProject}'.");
        }

        _reportWriter.Info("  Prompt regression gate: running test suite...");
        var result = await _processRunner.RunAsync(
            new ProcessSpec(
                "dotnet",
                ["test", testProject, "--no-build", "--configuration", "Release", "--verbosity", "quiet"],
                mcpToolsRoot),
            cancellationToken);

        if (result.Succeeded)
        {
            var summary = ExtractTestSummary(result.StandardOutput);
            return PromptRegressionGateResult.Pass(
                $"Prompt regression suite passed.{(summary.Length > 0 ? " " + summary : string.Empty)}");
        }

        var failureOutput = result.StandardOutput;
        var truncated = failureOutput.Length > 1000 ? failureOutput[..1000] + "..." : failureOutput;
        return PromptRegressionGateResult.Fail(
            $"Prompt regression suite failed (exit {result.ExitCode}):\n{truncated}");
    }

    /// <summary>
    /// Extracts the xUnit test result summary line from stdout, e.g.
    /// "Passed! - Failed: 0, Passed: 12, Skipped: 0, Total: 12, Duration: 1 s".
    /// </summary>
    private static string ExtractTestSummary(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var summaryLine = Array.FindLast(lines, l =>
            l.Contains("Passed!", StringComparison.OrdinalIgnoreCase) ||
            l.Contains("Total:", StringComparison.OrdinalIgnoreCase));
        return summaryLine?.Trim() ?? string.Empty;
    }
}
