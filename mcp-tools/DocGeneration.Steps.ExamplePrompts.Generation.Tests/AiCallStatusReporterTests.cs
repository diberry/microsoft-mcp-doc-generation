using ExamplePromptGeneratorStandalone.Utilities;
using Xunit;

namespace ExamplePromptGeneratorStandalone.Tests;

public sealed class AiCallStatusReporterTests
{
    [Theory]
    [InlineData("storage account list", "verbatim", "source-prompts")]
    [InlineData("keyvault secret get", "deterministic", "eligible-command")]
    public void WriteSkipped_IncludesProminentCallStatusAndReason(
        string command,
        string mode,
        string reason)
    {
        using var writer = new StringWriter();

        AiCallStatusReporter.WriteSkipped(writer, command, mode, reason);

        var output = writer.ToString();
        Assert.Contains("[Azure OpenAI] status=skipped", output);
        Assert.Contains("outcome=not-called", output);
        Assert.Contains("operation=GenerateExamplePrompts", output);
        Assert.Contains($"target=\"{command}\"", output);
        Assert.Contains($"mode={mode}", output);
        Assert.Contains($"reason={reason}", output);
    }
}
