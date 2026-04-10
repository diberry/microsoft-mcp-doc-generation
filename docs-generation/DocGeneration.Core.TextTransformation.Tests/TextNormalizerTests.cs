using Xunit;
using Azure.Mcp.TextTransformation.Models;
using Azure.Mcp.TextTransformation.Services;

namespace Azure.Mcp.TextTransformation.Tests;

public class TextNormalizerTests
{
    private readonly TransformationConfig _config;
    private readonly TextNormalizer _normalizer;

    public TextNormalizerTests()
    {
        _config = new TransformationConfig
        {
            Lexicon = new Lexicon
            {
                Acronyms = new Dictionary<string, AcronymEntry>
                {
                    { "id", new AcronymEntry { Canonical = "ID", Plural = "IDs" } },
                    { "vm", new AcronymEntry { Canonical = "VM", PreserveInTitleCase = true } }
                },
                Abbreviations = new Dictionary<string, AbbreviationEntry>(),
                StopWords = new List<string>()
            },
            Parameters = new ParameterConfig
            {
                Identifiers = new List<ParameterMapping>
                {
                    new ParameterMapping { Parameter = "database", Display = "Database name" },
                    new ParameterMapping { Parameter = "container", Display = "Container name" },
                    new ParameterMapping { Parameter = "account", Display = "Account name" }
                },
                Mappings = new List<ParameterMapping>
                {
                    new ParameterMapping { Parameter = "subscriptionId", Display = "subscription ID" },
                    new ParameterMapping { Parameter = "database", Display = "database (generic)" }
                }
            }
        };
        _normalizer = new TextNormalizer(_config);
    }

    // --- WrapExampleValues tests ---

    [Fact]
    public void WrapExampleValues_WrapsUnbacktickedValues()
    {
        var input = "Specify a duration (for example, PT1H) for the operation.";
        var result = _normalizer.WrapExampleValues(input);
        Assert.Equal("Specify a duration (for example, `PT1H`) for the operation.", result);
    }

    [Fact]
    public void WrapExampleValues_SkipsAlreadyBackticked()
    {
        var input = "Specify a duration (for example, `PT1H`) for the operation.";
        var result = _normalizer.WrapExampleValues(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void WrapExampleValues_HandlesMultipleValues()
    {
        var input = "Choose a format (for example, json, xml, yaml) for the output.";
        var result = _normalizer.WrapExampleValues(input);
        Assert.Equal("Choose a format (for example, `json`, `xml`, `yaml`) for the output.", result);
    }

    [Fact]
    public void WrapExampleValues_HandlesValueWithExplanation()
    {
        var input = "Set a retention period (for example, PT1H for 1 hour, P7D for 7 days) for logs.";
        var result = _normalizer.WrapExampleValues(input);
        Assert.Equal("Set a retention period (for example, `PT1H` for 1 hour, `P7D` for 7 days) for logs.", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void WrapExampleValues_NullOrEmpty_ReturnsInput(string? input)
    {
        var result = _normalizer.WrapExampleValues(input!);
        Assert.Equal(input, result);
    }

    [Fact]
    public void WrapExampleValues_NoPattern_ReturnsUnchanged()
    {
        var input = "This has no example pattern at all.";
        var result = _normalizer.WrapExampleValues(input);
        Assert.Equal(input, result);
    }

    // --- CleanAIGeneratedText tests ---

    [Fact]
    public void CleanAIGeneratedText_ReplacesSmartQuotes()
    {
        // \u2018 = left single, \u2019 = right single, \u201C = left double, \u201D = right double
        var input = "\u201CList all VMs\u201D and \u2018check status\u2019";
        var result = _normalizer.CleanAIGeneratedText(input);
        Assert.Equal("\"List all VMs\" and 'check status'", result);
    }

    [Fact]
    public void CleanAIGeneratedText_ReplacesHtmlEntities()
    {
        var input = "Use &quot;name&quot; &amp; &apos;value&apos; with &lt;tag&gt; and &#34;id&#34; plus &#39;key&#39;";
        var result = _normalizer.CleanAIGeneratedText(input);
        Assert.Equal("Use \"name\" & 'value' with <tag> and \"id\" plus 'key'", result);
    }

    [Fact]
    public void CleanAIGeneratedText_HandlesMixedContent()
    {
        var input = "\u201CShow &amp; tell\u201D";
        var result = _normalizer.CleanAIGeneratedText(input);
        Assert.Equal("\"Show & tell\"", result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CleanAIGeneratedText_NullOrEmpty_ReturnsInput(string? input)
    {
        var result = _normalizer.CleanAIGeneratedText(input!);
        Assert.Equal(input, result);
    }

    [Fact]
    public void CleanAIGeneratedText_CleanText_ReturnsUnchanged()
    {
        var input = "This is already clean text with \"quotes\" and 'apostrophes'.";
        var result = _normalizer.CleanAIGeneratedText(input);
        Assert.Equal(input, result);
    }

    // --- NormalizeParameter identifier priority tests ---

    [Fact]
    public void NormalizeParameter_ChecksIdentifiersFirst()
    {
        // "database" exists in both Identifiers and Mappings.
        // Identifiers should win: "Database name" not "database (generic)".
        var result = _normalizer.NormalizeParameter("database");
        Assert.Equal("Database name", result);
    }

    [Fact]
    public void NormalizeParameter_FallsBackToMappings()
    {
        // "subscriptionId" only exists in Mappings, not Identifiers.
        var result = _normalizer.NormalizeParameter("subscriptionId");
        Assert.Equal("subscription ID", result);
    }

    [Fact]
    public void NormalizeParameter_IdentifierWithCliPrefix()
    {
        // Should strip "--" prefix before lookup
        var result = _normalizer.NormalizeParameter("--container");
        Assert.Equal("Container name", result);
    }

    [Fact]
    public void NormalizeParameter_IdentifierCaseInsensitive()
    {
        var result = _normalizer.NormalizeParameter("Account");
        Assert.Equal("Account name", result);
    }

    [Fact]
    public void NormalizeParameter_NoIdentifierOrMapping_FallsThrough()
    {
        // "resourceGroupName" is not in identifiers or mappings — falls to hyphen split.
        // Since it has no hyphens, it stays as a single token (capitalized).
        // CamelCase splitting is available via SplitAndTransformProgrammaticName() for other uses.
        var result = _normalizer.NormalizeParameter("resourceGroupName");
        Assert.Equal("ResourceGroupName", result);
    }

    [Fact]
    public void NormalizeParameter_HyphenatedName_SplitsCorrectly()
    {
        // "resource-group-name" splits on hyphens for natural language output.
        var result = _normalizer.NormalizeParameter("resource-group-name");
        Assert.Equal("Resource group name", result);
    }
}
