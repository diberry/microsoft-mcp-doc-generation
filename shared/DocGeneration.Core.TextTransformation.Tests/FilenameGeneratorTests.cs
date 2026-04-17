using Xunit;
using Azure.Mcp.TextTransformation.Models;
using Azure.Mcp.TextTransformation.Services;

namespace Azure.Mcp.TextTransformation.Tests;

public class FilenameGeneratorTests
{
    private readonly TransformationConfig _config;
    private readonly FilenameGenerator _generator;

    public FilenameGeneratorTests()
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

    [Fact]
    public void GenerateFilename_Tier1_UsesBrandMapping()
    {
        var filename = _generator.GenerateFilename("aks", "get-cluster", "annotations");
        Assert.Equal("azure-kubernetes-service-get-cluster-annotations.md", filename);
    }

    [Fact]
    public void GenerateFilename_Tier2_UsesCompoundWords()
    {
        var filename = _generator.GenerateFilename("nodepool", "list", "parameters");
        Assert.Equal("node-pool-list-parameters.md", filename);
    }

    [Fact]
    public void GenerateFilename_Tier3_UsesOriginalName()
    {
        var filename = _generator.GenerateFilename("unknown", "operation", "type");
        Assert.Equal("unknown-operation-type.md", filename);
    }

    [Fact]
    public void CleanFilename_RemovesStopWords()
    {
        var cleaned = _generator.CleanFilename("get-a-list-of-the-items");
        Assert.Equal("get-list-items", cleaned);
    }

    [Fact]
    public void CleanFilename_LowercasesAcronyms_WhenCategoryDefaultApplies()
    {
        var cleaned = _generator.CleanFilename("AKS-cluster-VM");
        Assert.Equal("aks-cluster-vm", cleaned);
    }

    [Fact]
    public void GenerateMainServiceFilename_UsesBrandMappingFilename()
    {
        var filename = _generator.GenerateMainServiceFilename("aks");
        Assert.Equal("azure-kubernetes-service.md", filename);
    }

    [Fact]
    public void GenerateMainServiceFilename_UsesShortName_WhenFilenameNotSet()
    {
        var filename = _generator.GenerateMainServiceFilename("storage");
        Assert.Equal("storage.md", filename);
    }

    [Fact]
    public void GenerateMainServiceFilename_UsesOriginalName_WhenNoMapping()
    {
        var filename = _generator.GenerateMainServiceFilename("unknown");
        Assert.Equal("unknown.md", filename);
    }

    [Fact]
    public void GenerateFilename_WithEmptyAreaName_ReturnsEmpty()
    {
        var filename = _generator.GenerateFilename("");
        Assert.Equal(string.Empty, filename);
    }

    [Fact]
    public void CleanFilename_HandlesMultipleSeparators()
    {
        var cleaned = _generator.CleanFilename("some_mixed-name with spaces");
        Assert.Equal("some-mixed-name-with-spaces", cleaned);
    }
}
