using ExamplePromptGeneratorStandalone.Generators;
using Xunit;

namespace DocGeneration.Steps.ExamplePrompts.Generation.Tests;

public class ValidationFeedbackPromptTests
{
    [Fact]
    public void BuildValidationFeedbackSection_IncludesInstructionsAndReport()
    {
        const string feedback = "**Summary:** Missing required parameter 'subscription'\n- Issue: Use quoted placeholders";

        var section = ExamplePromptGenerator.BuildValidationFeedbackSection(feedback);

        Assert.Contains("Validation feedback from the previous attempt", section, StringComparison.Ordinal);
        Assert.Contains("correct every issue", section, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("```markdown", section, StringComparison.Ordinal);
        Assert.Contains(feedback, section, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildValidationFeedbackSection_WithRequiredParams_IncludesReminder()
    {
        const string feedback = "Missing 'server-name' in prompt 2";

        var section = ExamplePromptGenerator.BuildValidationFeedbackSection(
            feedback, requiredParamCount: "3", requiredParamNames: "server-name, database-name, resource-group");

        Assert.Contains("REMINDER", section, StringComparison.Ordinal);
        Assert.Contains("3 REQUIRED parameters", section, StringComparison.Ordinal);
        Assert.Contains("server-name, database-name, resource-group", section, StringComparison.Ordinal);
        Assert.Contains("Count them in each prompt", section, StringComparison.Ordinal);
    }
}
