using NUnit.Framework;
using Azure.Mcp.TextTransformation.Models;
using Azure.Mcp.TextTransformation.Services;

namespace Azure.Mcp.TextTransformation.Tests;

[TestFixture]
public class TransformationEngineTests
{
    private TransformationConfig _config = null!;
    private TransformationEngine _engine = null!;

    [SetUp]
    public void Setup()
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

    [Test]
    public void GetServiceDisplayName_WithMapping_ReturnsBrandName()
    {
        // Act
        var displayName = _engine.GetServiceDisplayName("aks");

        // Assert
        Assert.That(displayName, Is.EqualTo("Azure Kubernetes Service"));
    }

    [Test]
    public void GetServiceDisplayName_WithoutMapping_ReturnsTitleCase()
    {
        // Act
        var displayName = _engine.GetServiceDisplayName("storage");

        // Assert
        Assert.That(displayName, Is.EqualTo("Storage"));
    }

    [Test]
    public void GetServiceShortName_WithMapping_ReturnsShortName()
    {
        // Act
        var shortName = _engine.GetServiceShortName("aks");

        // Assert
        Assert.That(shortName, Is.EqualTo("AKS"));
    }

    [Test]
    public void GetServiceShortName_WithoutMapping_ReturnsMcpName()
    {
        // Act
        var shortName = _engine.GetServiceShortName("storage");

        // Assert
        Assert.That(shortName, Is.EqualTo("storage"));
    }

    [Test]
    public void TransformDescription_ReplacesAbbreviations()
    {
        // Arrange
        var description = "This is an example eg a test";

        // Act
        var transformed = _engine.TransformDescription(description);

        // Assert
        Assert.That(transformed, Does.Contain("e.g."));
    }

    [Test]
    public void TransformDescription_EnsuresEndsPeriod()
    {
        // Arrange
        var description = "This is a test";

        // Act
        var transformed = _engine.TransformDescription(description);

        // Assert
        Assert.That(transformed, Does.EndWith("."));
    }

    [Test]
    public void TransformDescription_DoesNotAddPeriod_WhenAlreadyPresent()
    {
        // Arrange
        var description = "This is a test.";

        // Act
        var transformed = _engine.TransformDescription(description);

        // Assert
        Assert.That(transformed, Is.EqualTo("This is a test."));
    }

    [Test]
    public void TextNormalizer_NormalizeParameter_UsesMappingWhenAvailable()
    {
        // Act
        var normalized = _engine.TextNormalizer.NormalizeParameter("subscriptionId");

        // Assert
        Assert.That(normalized, Is.EqualTo("subscription ID"));
    }

    [Test]
    public void TextNormalizer_SplitAndTransformProgrammaticName_SplitsCamelCase()
    {
        // Act
        var transformed = _engine.TextNormalizer.SplitAndTransformProgrammaticName("resourceGroupName");

        // Assert
        Assert.That(transformed, Is.EqualTo("resource group name"));
    }

    [Test]
    public void TextNormalizer_SplitAndTransformProgrammaticName_HandlesAcronyms()
    {
        // Act
        var transformed = _engine.TextNormalizer.SplitAndTransformProgrammaticName("vmId");

        // Assert
        Assert.That(transformed, Is.EqualTo("VM ID"));
    }

    [Test]
    public void TextNormalizer_ToTitleCase_PreservesAcronyms()
    {
        // Act
        var titleCase = _engine.TextNormalizer.ToTitleCase("get vm id");

        // Assert
        Assert.That(titleCase, Is.EqualTo("Get VM ID"));
    }

    [Test]
    public void TextNormalizer_ToTitleCase_LowercasesStopWords()
    {
        // Act
        var titleCase = _engine.TextNormalizer.ToTitleCase("get a list of the items", "titleCase");

        // Assert
        Assert.That(titleCase, Is.EqualTo("Get a List of the Items"));
    }

    [Test]
    public void TextNormalizer_ToTitleCase_CapitalizesFirstStopWord()
    {
        // Act
        var titleCase = _engine.TextNormalizer.ToTitleCase("a list of items", "titleCase");

        // Assert
        Assert.That(titleCase, Is.EqualTo("A List of Items"));
    }

    [Test]
    public void TextNormalizer_ReplaceStaticText_HandlesMultipleReplacements()
    {
        // Arrange
        var text = "Use eg for examples";

        // Act
        var replaced = _engine.TextNormalizer.ReplaceStaticText(text);

        // Assert
        Assert.That(replaced, Does.Contain("e.g."));
    }

    [Test]
    public void Config_IsAccessible()
    {
        // Assert
        Assert.That(_engine.Config, Is.SameAs(_config));
    }
}
