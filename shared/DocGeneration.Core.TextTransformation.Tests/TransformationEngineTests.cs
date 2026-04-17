using Xunit;
using Azure.Mcp.TextTransformation.Models;
using Azure.Mcp.TextTransformation.Services;

namespace Azure.Mcp.TextTransformation.Tests;

public class TransformationEngineTests
{
    private readonly TransformationConfig _config;
    private readonly TransformationEngine _engine;

    public TransformationEngineTests()
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
                Abbreviations = new Dictionary<string, AbbreviationEntry>
                {
                    { "eg", new AbbreviationEntry { Canonical = "e.g.", Expansion = "for example" } }
                },
                StopWords = new List<string> { "a", "the", "or", "and", "in", "of" }
            },
            Services = new ServiceConfig
            {
                Mappings = new List<ServiceMapping>
                {
                    new ServiceMapping
                    {
                        McpName = "aks",
                        ShortName = "AKS",
                        BrandName = "Azure Kubernetes Service"
                    }
                }
            },
            Contexts = new Dictionary<string, ContextRules>
            {
                { "titleCase", new ContextRules { Rules = new Dictionary<string, string> { { "stopWords", "lowercase-unless-first" } } } }
            },
            Parameters = new ParameterConfig
            {
                Mappings = new List<ParameterMapping>
                {
                    new ParameterMapping { Parameter = "subscriptionId", Display = "subscription ID" }
                }
            }
        };
        _engine = new TransformationEngine(_config);
    }

    [Fact]
    public void GetServiceDisplayName_WithMapping_ReturnsBrandName()
    {
        Assert.Equal("Azure Kubernetes Service", _engine.GetServiceDisplayName("aks"));
    }

    [Fact]
    public void GetServiceDisplayName_WithoutMapping_ReturnsTitleCase()
    {
        Assert.Equal("Storage", _engine.GetServiceDisplayName("storage"));
    }

    [Fact]
    public void GetServiceShortName_WithMapping_ReturnsShortName()
    {
        Assert.Equal("AKS", _engine.GetServiceShortName("aks"));
    }

    [Fact]
    public void GetServiceShortName_WithoutMapping_ReturnsMcpName()
    {
        Assert.Equal("storage", _engine.GetServiceShortName("storage"));
    }

    [Fact]
    public void TransformDescription_ReplacesAbbreviations()
    {
        var transformed = _engine.TransformDescription("This is an example eg a test");
        Assert.Contains("e.g.", transformed);
    }

    [Fact]
    public void TransformDescription_EnsuresEndsPeriod()
    {
        var transformed = _engine.TransformDescription("This is a test");
        Assert.EndsWith(".", transformed);
    }

    [Fact]
    public void TransformDescription_DoesNotAddPeriod_WhenAlreadyPresent()
    {
        var transformed = _engine.TransformDescription("This is a test.");
        Assert.Equal("This is a test.", transformed);
    }

    [Fact]
    public void TextNormalizer_NormalizeParameter_UsesMappingWhenAvailable()
    {
        Assert.Equal("subscription ID", _engine.TextNormalizer.NormalizeParameter("subscriptionId"));
    }

    [Fact]
    public void TextNormalizer_SplitAndTransformProgrammaticName_SplitsCamelCase()
    {
        Assert.Equal("resource group name", _engine.TextNormalizer.SplitAndTransformProgrammaticName("resourceGroupName"));
    }

    [Fact]
    public void TextNormalizer_SplitAndTransformProgrammaticName_HandlesAcronyms()
    {
        Assert.Equal("VM ID", _engine.TextNormalizer.SplitAndTransformProgrammaticName("vmId"));
    }

    [Fact]
    public void TextNormalizer_ToTitleCase_PreservesAcronyms()
    {
        Assert.Equal("Get VM ID", _engine.TextNormalizer.ToTitleCase("get vm id"));
    }

    [Fact]
    public void TextNormalizer_ToTitleCase_LowercasesStopWords()
    {
        Assert.Equal("Get a List of the Items", _engine.TextNormalizer.ToTitleCase("get a list of the items", "titleCase"));
    }

    [Fact]
    public void TextNormalizer_ToTitleCase_CapitalizesFirstStopWord()
    {
        Assert.Equal("A List of Items", _engine.TextNormalizer.ToTitleCase("a list of items", "titleCase"));
    }

    [Fact]
    public void TextNormalizer_ReplaceStaticText_HandlesMultipleReplacements()
    {
        var replaced = _engine.TextNormalizer.ReplaceStaticText("Use eg for examples");
        Assert.Contains("e.g.", replaced);
    }

    [Fact]
    public void Config_IsAccessible()
    {
        Assert.Same(_config, _engine.Config);
    }
}
