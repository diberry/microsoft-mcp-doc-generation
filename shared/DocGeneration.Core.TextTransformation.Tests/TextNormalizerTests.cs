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

    [Theory]
    [InlineData("deployment", "Deployment name")]
    [InlineData("knowledge-base", "Knowledge base name")]
    [InlineData("knowledge-source", "Knowledge source name")]
    [InlineData("webtest-resource", "Web test resource name")]
    [InlineData("health-model", "Health model name")]
    [InlineData("network-security-group", "Network security group name")]
    [InlineData("public-ip-address", "Public IP address name")]
    [InlineData("virtual-network", "Virtual network name")]
    public void NormalizeParameter_MultiWordResourceIdentifier_AppendsNameSuffix(string input, string expected)
    {
        var config = new TransformationConfig
        {
            Lexicon = new Lexicon
            {
                Acronyms = new Dictionary<string, AcronymEntry>
                {
                    { "ip", new AcronymEntry { Canonical = "IP" } }
                },
                Abbreviations = new Dictionary<string, AbbreviationEntry>(),
                StopWords = new List<string>()
            },
            Parameters = new ParameterConfig
            {
                Identifiers = new List<ParameterMapping>
                {
                    new ParameterMapping { Parameter = "deployment", Display = "Deployment name" },
                    new ParameterMapping { Parameter = "knowledge-base", Display = "Knowledge base name" },
                    new ParameterMapping { Parameter = "knowledge-source", Display = "Knowledge source name" },
                    new ParameterMapping { Parameter = "webtest-resource", Display = "Web test resource name" },
                    new ParameterMapping { Parameter = "health-model", Display = "Health model name" },
                    new ParameterMapping { Parameter = "network-security-group", Display = "Network security group name" },
                    new ParameterMapping { Parameter = "public-ip-address", Display = "Public IP address name" },
                    new ParameterMapping { Parameter = "virtual-network", Display = "Virtual network name" }
                },
                Mappings = new List<ParameterMapping>()
            }
        };
        var normalizer = new TextNormalizer(config);
        var result = normalizer.NormalizeParameter(input);
        Assert.Equal(expected, result);
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

    // --- EnsureEndsPeriod tests (ported from NaturalLanguage.Tests) ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EnsureEndsPeriod_NullOrEmpty_ReturnsSame(string? input)
    {
        var result = _normalizer.EnsureEndsPeriod(input!);
        Assert.Equal(input, result);
    }

    [Fact]
    public void EnsureEndsPeriod_TextWithoutPeriod_AddsPeriod()
    {
        var result = _normalizer.EnsureEndsPeriod("The Azure subscription ID to use");
        Assert.Equal("The Azure subscription ID to use.", result);
    }

    [Fact]
    public void EnsureEndsPeriod_TextAlreadyEndingWithPeriod_NoChange()
    {
        var result = _normalizer.EnsureEndsPeriod("Specifies the Azure resource group.");
        Assert.Equal("Specifies the Azure resource group.", result);
    }

    [Fact]
    public void EnsureEndsPeriod_TextEndingWithQuestionMark_NoChange()
    {
        var result = _normalizer.EnsureEndsPeriod("Is the resource currently available?");
        Assert.Equal("Is the resource currently available?", result);
    }

    [Fact]
    public void EnsureEndsPeriod_TextEndingWithExclamation_NoChange()
    {
        var result = _normalizer.EnsureEndsPeriod("Resource provisioning failed!");
        Assert.Equal("Resource provisioning failed!", result);
    }

    [Fact]
    public void EnsureEndsPeriod_Idempotent_DoesNotAddDoublePeriod()
    {
        var input = "The Azure subscription ID to use";
        var first = _normalizer.EnsureEndsPeriod(input);
        var second = _normalizer.EnsureEndsPeriod(first);
        Assert.Equal("The Azure subscription ID to use.", second);
    }

    [Fact]
    public void EnsureEndsPeriod_TrimsWhitespace_BeforeCheck()
    {
        var result = _normalizer.EnsureEndsPeriod("  The Cosmos DB account name  ");
        Assert.Equal("The Cosmos DB account name.", result);
    }

    [Fact]
    public void EnsureEndsPeriod_QuestionMarkBeforeSingleQuote_NoChange()
    {
        var result = _normalizer.EnsureEndsPeriod("Ask 'Why are requests timing out?'");
        Assert.Equal("Ask 'Why are requests timing out?'", result);
    }

    [Fact]
    public void EnsureEndsPeriod_QuestionMarkBeforeDoubleQuote_NoChange()
    {
        var result = _normalizer.EnsureEndsPeriod("Ask \"What is the status?\"");
        Assert.Equal("Ask \"What is the status?\"", result);
    }

    [Fact]
    public void EnsureEndsPeriod_ExclamationBeforeDoubleQuote_NoChange()
    {
        var result = _normalizer.EnsureEndsPeriod("Alert said \"Critical failure!\"");
        Assert.Equal("Alert said \"Critical failure!\"", result);
    }

    [Fact]
    public void EnsureEndsPeriod_PeriodBeforeSingleQuote_NoChange()
    {
        var result = _normalizer.EnsureEndsPeriod("Run command 'az show.'");
        Assert.Equal("Run command 'az show.'", result);
    }

    [Fact]
    public void EnsureEndsPeriod_NoPunctuationBeforeSingleQuote_AddsPeriod()
    {
        var result = _normalizer.EnsureEndsPeriod("Use resource 'my-resource'");
        Assert.Equal("Use resource 'my-resource'.", result);
    }

    [Fact]
    public void EnsureEndsPeriod_NoPunctuationBeforeDoubleQuote_AddsPeriod()
    {
        var result = _normalizer.EnsureEndsPeriod("Show vault \"my-vault\"");
        Assert.Equal("Show vault \"my-vault\".", result);
    }

    [Fact]
    public void EnsureEndsPeriod_QuestionMarkBeforeBacktick_NoChange()
    {
        var result = _normalizer.EnsureEndsPeriod("Ask `Why is latency high?`");
        Assert.Equal("Ask `Why is latency high?`", result);
    }

    [Fact]
    public void EnsureEndsPeriod_NoPunctuationBeforeBacktick_AddsPeriod()
    {
        var result = _normalizer.EnsureEndsPeriod("Run `az account show`");
        Assert.Equal("Run `az account show`.", result);
    }
}
