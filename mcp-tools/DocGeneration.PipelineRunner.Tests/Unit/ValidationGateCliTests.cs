using PipelineRunner.Cli;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// CLI parsing tests for the --run-fingerprint-gate and --run-prompt-regression-gate flags.
/// </summary>
public class ValidationGateCliTests
{
    // ── --run-fingerprint-gate ─────────────────────────────────────────────

    [Fact]
    public void Parse_RunFingerprintGateFlag_ParsedAsTrue()
    {
        var result = PipelineCli.Parse(["--namespace", "compute", "--run-fingerprint-gate"]);

        Assert.NotNull(result.Request);
        Assert.True(result.Request!.RunFingerprintGate);
    }

    [Fact]
    public void Parse_NoRunFingerprintGateFlag_DefaultsFalse()
    {
        var result = PipelineCli.Parse(["--namespace", "compute"]);

        Assert.NotNull(result.Request);
        Assert.False(result.Request!.RunFingerprintGate);
    }

    // ── --run-prompt-regression-gate ───────────────────────────────────────

    [Fact]
    public void Parse_RunPromptRegressionGateFlag_ParsedAsTrue()
    {
        var result = PipelineCli.Parse(["--namespace", "compute", "--run-prompt-regression-gate"]);

        Assert.NotNull(result.Request);
        Assert.True(result.Request!.RunPromptRegressionGate);
    }

    [Fact]
    public void Parse_NoRunPromptRegressionGateFlag_DefaultsFalse()
    {
        var result = PipelineCli.Parse(["--namespace", "compute"]);

        Assert.NotNull(result.Request);
        Assert.False(result.Request!.RunPromptRegressionGate);
    }

    // ── Both flags together ────────────────────────────────────────────────

    [Fact]
    public void Parse_BothValidationGateFlags_BothParsedCorrectly()
    {
        var result = PipelineCli.Parse([
            "--namespace", "storage",
            "--run-fingerprint-gate",
            "--run-prompt-regression-gate",
            "--skip-build"
        ]);

        Assert.NotNull(result.Request);
        Assert.True(result.Request!.RunFingerprintGate);
        Assert.True(result.Request.RunPromptRegressionGate);
        Assert.True(result.Request.SkipBuild);
    }

    // ── Default state of all flags ─────────────────────────────────────────

    [Fact]
    public void Parse_NoValidationGateFlags_BothDefaultFalse()
    {
        var result = PipelineCli.Parse(["--namespace", "advisor", "--skip-changelog-gate"]);

        Assert.NotNull(result.Request);
        Assert.False(result.Request!.RunFingerprintGate);
        Assert.False(result.Request!.RunPromptRegressionGate);
    }
}
