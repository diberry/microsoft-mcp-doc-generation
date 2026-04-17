using Xunit;
using ExamplePromptGeneratorStandalone.Generators;

namespace ExamplePromptGeneratorStandalone.Tests;

public class ExtractJsonFromLLMResponseTests
{
    // ─────────────────────────────────────────────────
    // Strategy 1: ```json code block
    // ─────────────────────────────────────────────────

    [Fact]
    public void ExtractsJson_FromJsonCodeBlock()
    {
        var response = """
            Some reasoning text here.
            ```json
            {"tool_name": ["prompt 1", "prompt 2"]}
            ```
            """;

        var result = ExamplePromptGenerator.ExtractJsonFromLLMResponse(response);

        Assert.Equal("""{"tool_name": ["prompt 1", "prompt 2"]}""", result);
    }

    [Fact]
    public void ExtractsJson_FromJsonCodeBlock_WithMultilineJson()
    {
        var response = """
            STEP 1: Analyzed parameters.
            STEP 2: Generated prompts.
            ```json
            {
              "storage account list": [
                "List all storage accounts in my subscription",
                "Show me the storage accounts in resource group rg-prod"
              ]
            }
            ```
            """;

        var result = ExamplePromptGenerator.ExtractJsonFromLLMResponse(response);

        Assert.Contains("storage account list", result);
        Assert.StartsWith("{", result);
        Assert.EndsWith("}", result);
    }

    // ─────────────────────────────────────────────────
    // Strategy 2: Last ``` code block
    // ─────────────────────────────────────────────────

    [Fact]
    public void ExtractsJson_FromLastCodeBlock_WhenNoJsonTag()
    {
        var response = """
            Here is some preamble.
            ```
            not json content
            ```
            And more text.
            ```
            {"advisor recommendation list": ["prompt 1"]}
            ```
            """;

        var result = ExamplePromptGenerator.ExtractJsonFromLLMResponse(response);

        Assert.Contains("advisor recommendation list", result);
    }

    // ─────────────────────────────────────────────────
    // Strategy 3: Brace matching
    // ─────────────────────────────────────────────────

    [Fact]
    public void ExtractsJson_UsingBraceMatching_WhenNoCodeBlocks()
    {
        var response = """
            I've analyzed the tool parameters and here are the prompts:
            {"keyvault secret list": ["List all secrets in my key vault", "Show secrets in vault myvault"]}
            """;

        var result = ExamplePromptGenerator.ExtractJsonFromLLMResponse(response);

        Assert.Contains("keyvault secret list", result);
        Assert.StartsWith("{", result);
        Assert.EndsWith("}", result);
    }

    [Fact]
    public void ExtractsJson_WithNestedBraces()
    {
        var response = """
            Some text with a random { character.
            {"tool": ["prompt with {placeholder} inside"]}
            """;

        var result = ExamplePromptGenerator.ExtractJsonFromLLMResponse(response);

        Assert.StartsWith("{", result);
        Assert.EndsWith("}", result);
    }

    // ─────────────────────────────────────────────────
    // Edge cases
    // ─────────────────────────────────────────────────

    [Fact]
    public void ReturnsEmpty_ForNullInput()
    {
        var result = ExamplePromptGenerator.ExtractJsonFromLLMResponse(null!);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ReturnsEmpty_ForEmptyInput()
    {
        var result = ExamplePromptGenerator.ExtractJsonFromLLMResponse("");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ReturnsEmpty_WhenNoJsonFound()
    {
        var result = ExamplePromptGenerator.ExtractJsonFromLLMResponse("Just plain text with no JSON at all.");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExtractsJson_PureJsonInput()
    {
        var json = """{"cosmos db container list": ["List containers in my database"]}""";

        var result = ExamplePromptGenerator.ExtractJsonFromLLMResponse(json);

        Assert.Equal(json, result);
    }

    [Fact]
    public void ExtractsJson_WithLLMPreambleSteps()
    {
        var response = """
            STEP 1: I identified the required parameters: --vault-name (Required)
            STEP 2: I verified all prompts include vault-name
            STEP 3: Generating 5 prompts

            ```json
            {"keyvault key list": ["List all keys in vault myvault", "Show me the keys stored in key vault production-vault"]}
            ```

            VERIFICATION: All prompts contain the required --vault-name parameter. ✅
            """;

        var result = ExamplePromptGenerator.ExtractJsonFromLLMResponse(response);

        Assert.Contains("keyvault key list", result);
        Assert.DoesNotContain("STEP", result);
        Assert.DoesNotContain("VERIFICATION", result);
    }
}
