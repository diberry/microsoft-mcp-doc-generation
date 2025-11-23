using NUnit.Framework;
using Azure.Mcp.TextTransformation.Models;

namespace Azure.Mcp.TextTransformation.Tests;

[TestFixture]
public class ConfigLoaderTests
{
    private string _testConfigPath = null!;

    [SetUp]
    public void Setup()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), "test-transformation-config.json");
    }

    [TearDown]
    public void Teardown()
    {
        if (File.Exists(_testConfigPath))
        {
            File.Delete(_testConfigPath);
        }
    }

    [Test]
    public async Task LoadAsync_WithValidConfig_LoadsSuccessfully()
    {
        // Arrange
        var configJson = @"{
            ""lexicon"": {
                ""acronyms"": {
                    ""id"": {
                        ""canonical"": ""ID"",
                        ""plural"": ""IDs""
                    }
                },
                ""stopWords"": [""a"", ""the""]
            },
            ""services"": {
                ""mappings"": []
            }
        }";
        await File.WriteAllTextAsync(_testConfigPath, configJson);
        var loader = new ConfigLoader(_testConfigPath);

        // Act
        var config = await loader.LoadAsync();

        // Assert
        Assert.That(config, Is.Not.Null);
        Assert.That(config.Lexicon.Acronyms.ContainsKey("id"), Is.True);
        Assert.That(config.Lexicon.Acronyms["id"].Canonical, Is.EqualTo("ID"));
        Assert.That(config.Lexicon.StopWords.Count, Is.EqualTo(2));
    }

    [Test]
    public void LoadAsync_WithMissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var loader = new ConfigLoader("/nonexistent/path/config.json");

        // Act & Assert
        Assert.ThrowsAsync<FileNotFoundException>(async () => await loader.LoadAsync());
    }

    [Test]
    public async Task LoadAsync_ResolvesLexiconReferences()
    {
        // Arrange
        var configJson = @"{
            ""lexicon"": {
                ""acronyms"": {
                    ""aks"": {
                        ""canonical"": ""AKS"",
                        ""expansion"": ""Azure Kubernetes Service""
                    }
                }
            },
            ""services"": {
                ""mappings"": [
                    {
                        ""mcpName"": ""aks"",
                        ""shortName"": ""$lexicon.acronyms.aks""
                    }
                ]
            }
        }";
        await File.WriteAllTextAsync(_testConfigPath, configJson);
        var loader = new ConfigLoader(_testConfigPath);

        // Act
        var config = await loader.LoadAsync();

        // Assert
        Assert.That(config.Services.Mappings[0].ShortName, Is.EqualTo("AKS"));
    }

    [Test]
    public async Task LoadAsync_CachesConfiguration()
    {
        // Arrange
        var configJson = @"{""lexicon"":{""stopWords"":[]},""services"":{""mappings"":[]}}";
        await File.WriteAllTextAsync(_testConfigPath, configJson);
        var loader = new ConfigLoader(_testConfigPath);

        // Act
        var config1 = await loader.LoadAsync();
        var config2 = await loader.LoadAsync();

        // Assert
        Assert.That(config1, Is.SameAs(config2));
    }
}
