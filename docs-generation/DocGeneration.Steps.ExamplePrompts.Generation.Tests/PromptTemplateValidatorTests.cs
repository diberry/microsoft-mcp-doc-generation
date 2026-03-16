using Xunit;
using ExamplePromptGeneratorStandalone.Utilities;

namespace ExamplePromptGeneratorStandalone.Tests;

public class PromptTemplateValidatorTests
{
    // --- Validate: valid prompts ---

    [Fact]
    public void Validate_FullyResolvedPrompt_ReturnsEmpty()
    {
        var prompt = """
            You are generating example prompts for the tool "acr_registry_list".
            Tool command: acr registry list
            Description: List all container registries in a subscription.
            Action verb: List
            Resource type: container registries
            Generate 5 prompts now.
            """;

        var problems = PromptTemplateValidator.Validate(prompt);
        Assert.Empty(problems);
    }

    [Fact]
    public void Validate_PromptWithJsonCurlyBraces_ReturnsEmpty()
    {
        // JSON content in prompt should NOT trigger false positives
        var prompt = """
            Respond with JSON like this:
            {
              "prompts": [
                "List all storage accounts",
                "Show blob containers"
              ]
            }
            """;

        var problems = PromptTemplateValidator.Validate(prompt);
        Assert.Empty(problems);
    }

    [Fact]
    public void Validate_PromptWithAngleBracketPlaceholders_ReturnsEmpty()
    {
        // <subscription>, <resource-group> are intentional runtime placeholders
        var prompt = "List key vaults in <subscription> and <resource-group>";

        var problems = PromptTemplateValidator.Validate(prompt);
        Assert.Empty(problems);
    }

    // --- Validate: unreplaced simple placeholders ---

    [Theory]
    [InlineData("{TOOL_NAME}")]
    [InlineData("{TOOL_COMMAND}")]
    [InlineData("{TOOL_DESCRIPTION}")]
    [InlineData("{ACTION_VERB}")]
    [InlineData("{RESOURCE_TYPE}")]
    [InlineData("{PROMPT_COUNT}")]
    public void Validate_UnreplacedSimplePlaceholder_ReturnsProblem(string placeholder)
    {
        var prompt = $"Generate prompts for {placeholder} using the tool.";

        var problems = PromptTemplateValidator.Validate(prompt);

        Assert.Single(problems);
        Assert.Contains(placeholder, problems[0]);
    }

    [Fact]
    public void Validate_MultipleUnreplacedPlaceholders_ReturnsAll()
    {
        var prompt = "Tool: {TOOL_NAME}, Command: {TOOL_COMMAND}, Verb: {ACTION_VERB}";

        var problems = PromptTemplateValidator.Validate(prompt);

        Assert.Equal(3, problems.Count);
        Assert.Contains(problems, p => p.Contains("{TOOL_NAME}"));
        Assert.Contains(problems, p => p.Contains("{TOOL_COMMAND}"));
        Assert.Contains(problems, p => p.Contains("{ACTION_VERB}"));
    }

    // --- Validate: unreplaced Handlebars blocks ---

    [Fact]
    public void Validate_UnreplacedEachBlock_ReturnsProblem()
    {
        var prompt = "Parameters:\n{{#each PARAMETERS}}\n- {{name}}: {{description}}\n{{/each}}";

        var problems = PromptTemplateValidator.Validate(prompt);

        Assert.True(problems.Count >= 2);
        Assert.Contains(problems, p => p.Contains("{{#each PARAMETERS}}"));
        Assert.Contains(problems, p => p.Contains("{{/each}}"));
    }

    [Fact]
    public void Validate_UnreplacedHandlebarsExpression_ReturnsProblem()
    {
        var prompt = "The parameter {{name}} has description {{description}}.";

        var problems = PromptTemplateValidator.Validate(prompt);

        Assert.Equal(2, problems.Count);
        Assert.Contains(problems, p => p.Contains("{{name}}"));
        Assert.Contains(problems, p => p.Contains("{{description}}"));
    }

    [Fact]
    public void Validate_UnreplacedIfBlock_ReturnsProblem()
    {
        var prompt = "{{#if hasParams}}\nShow params\n{{/if}}";

        var problems = PromptTemplateValidator.Validate(prompt);

        Assert.Contains(problems, p => p.Contains("{{#if hasParams}}"));
        Assert.Contains(problems, p => p.Contains("{{/if}}"));
    }

    // --- Validate: edge cases ---

    [Fact]
    public void Validate_NullPrompt_ReturnsProblem()
    {
        var problems = PromptTemplateValidator.Validate(null!);
        Assert.Single(problems);
        Assert.Contains("null or empty", problems[0]);
    }

    [Fact]
    public void Validate_EmptyPrompt_ReturnsProblem()
    {
        var problems = PromptTemplateValidator.Validate(string.Empty);
        Assert.Single(problems);
        Assert.Contains("null or empty", problems[0]);
    }

    [Fact]
    public void Validate_MixedPlaceholdersAndHandlebarsBlocks_ReturnsAll()
    {
        var prompt = "Tool: {TOOL_NAME}\n{{#each PARAMETERS}}\n{{name}}\n{{/each}}";

        var problems = PromptTemplateValidator.Validate(prompt);

        Assert.True(problems.Count >= 4);
        Assert.Contains(problems, p => p.Contains("{TOOL_NAME}"));
        Assert.Contains(problems, p => p.Contains("{{#each PARAMETERS}}"));
        Assert.Contains(problems, p => p.Contains("{{name}}"));
        Assert.Contains(problems, p => p.Contains("{{/each}}"));
    }

    // --- ValidateAndLog ---

    [Fact]
    public void ValidateAndLog_ValidPrompt_ReturnsTrue()
    {
        var prompt = "A fully resolved prompt with no placeholders.";
        Assert.True(PromptTemplateValidator.ValidateAndLog(prompt, "test_tool"));
    }

    [Fact]
    public void ValidateAndLog_InvalidPrompt_ReturnsFalse()
    {
        var prompt = "Prompt with {TOOL_NAME} unreplaced.";
        Assert.False(PromptTemplateValidator.ValidateAndLog(prompt, "test_tool"));
    }
}
