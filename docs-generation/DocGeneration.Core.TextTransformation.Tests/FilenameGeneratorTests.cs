using NUnit.Framework;
using Azure.Mcp.TextTransformation.Models;
using Azure.Mcp.TextTransformation.Services;

namespace Azure.Mcp.TextTransformation.Tests;

[TestFixture]
public class FilenameGeneratorTests
{
    private TransformationConfig _config = null!;
    private FilenameGenerator _generator = null!;

    [SetUp]
    public void Setup()
    {
        _config = new TransformationConfig
        {
            Lexicon = new Lexicon
            {
                Acronyms = new Dictionary<string, AcronymEntry>
                {
                    { "aks", new AcronymEntry { Canonical = "AKS" } },
                    { "vm", new AcronymEntry { Canonical = "VM" } }
                },
                CompoundWords = new Dictionary<string, CompoundWordEntry>
                {
                    { "nodepool", new CompoundWordEntry { Components = new List<string> { "node", "pool" } } }
                },
                StopWords = new List<string> { "a", "the", "or", "and", "of" }
            },
            Services = new ServiceConfig
            {
                Mappings = new List<ServiceMapping>
                {
                    new ServiceMapping 
                    { 
                        McpName = "aks", 
                        ShortName = "aks",
                        Filename = "azure-kubernetes-service"
                    },
                    new ServiceMapping
                    {
                        McpName = "storage",
                        ShortName = "storage"
                    }
                }
            },
            Contexts = new Dictionary<string, ContextRules>
            {
                { "filename", new ContextRules { Rules = new Dictionary<string, string> { { "stopWords", "remove" } } } }
            },
            CategoryDefaults = new Dictionary<string, CategoryDefaults>
            {
                { "acronym", new CategoryDefaults { FilenameTransform = "to-lowercase" } }
            }
        };
        _generator = new FilenameGenerator(_config);
    }

    [Test]
    public void GenerateFilename_Tier1_UsesBrandMapping()
    {
        // Act
        var filename = _generator.GenerateFilename("aks", "get-cluster", "annotations");

        // Assert
        Assert.That(filename, Is.EqualTo("azure-kubernetes-service-get-cluster-annotations.md"));
    }

    [Test]
    public void GenerateFilename_Tier2_UsesCompoundWords()
    {
        // Act
        var filename = _generator.GenerateFilename("nodepool", "list", "parameters");

        // Assert
        Assert.That(filename, Is.EqualTo("node-pool-list-parameters.md"));
    }

    [Test]
    public void GenerateFilename_Tier3_UsesOriginalName()
    {
        // Act
        var filename = _generator.GenerateFilename("unknown", "operation", "type");

        // Assert
        Assert.That(filename, Is.EqualTo("unknown-operation-type.md"));
    }

    [Test]
    public void CleanFilename_RemovesStopWords()
    {
        // Act
        var cleaned = _generator.CleanFilename("get-a-list-of-the-items");

        // Assert
        Assert.That(cleaned, Is.EqualTo("get-list-items"));
    }

    [Test]
    public void CleanFilename_LowercasesAcronyms_WhenCategoryDefaultApplies()
    {
        // Act
        var cleaned = _generator.CleanFilename("AKS-cluster-VM");

        // Assert
        Assert.That(cleaned, Is.EqualTo("aks-cluster-vm"));
    }

    [Test]
    public void GenerateMainServiceFilename_UsesBrandMappingFilename()
    {
        // Act
        var filename = _generator.GenerateMainServiceFilename("aks");

        // Assert
        Assert.That(filename, Is.EqualTo("azure-kubernetes-service.md"));
    }

    [Test]
    public void GenerateMainServiceFilename_UsesShortName_WhenFilenameNotSet()
    {
        // Act
        var filename = _generator.GenerateMainServiceFilename("storage");

        // Assert
        Assert.That(filename, Is.EqualTo("storage.md"));
    }

    [Test]
    public void GenerateMainServiceFilename_UsesOriginalName_WhenNoMapping()
    {
        // Act
        var filename = _generator.GenerateMainServiceFilename("unknown");

        // Assert
        Assert.That(filename, Is.EqualTo("unknown.md"));
    }

    [Test]
    public void GenerateFilename_WithEmptyAreaName_ReturnsEmpty()
    {
        // Act
        var filename = _generator.GenerateFilename("");

        // Assert
        Assert.That(filename, Is.EqualTo(string.Empty));
    }

    [Test]
    public void CleanFilename_HandlesMultipleSeparators()
    {
        // Act
        var cleaned = _generator.CleanFilename("some_mixed-name with spaces");

        // Assert
        Assert.That(cleaned, Is.EqualTo("some-mixed-name-with-spaces"));
    }
}
