using Xunit;
using Azure.Mcp.TextTransformation.Models;

namespace Azure.Mcp.TextTransformation.Tests;

public class ConfigLoaderTests : IDisposable
{
    private readonly string _testConfigPath;

    public ConfigLoaderTests()
    {
        _testConfigPath = Path.Combine(Path.GetTempPath(), "test-transformation-config.json");
    }

    public void Dispose()
    {
        if (File.Exists(_testConfigPath))
        {
            File.Delete(_testConfigPath);
        }
    }

    [Fact]
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
        Assert.NotNull(config);
        Assert.True(config.Lexicon.Acronyms.ContainsKey("id"));
        Assert.Equal("ID", config.Lexicon.Acronyms["id"].Canonical);
        Assert.Equal(2, config.Lexicon.StopWords.Count);
    }

    [Fact]
    public async Task LoadAsync_WithMissingFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var loader = new ConfigLoader("/nonexistent/path/config.json");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () => await loader.LoadAsync());
    }

    [Fact]
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
        Assert.Equal("AKS", config.Services.Mappings[0].ShortName);
    }

    [Fact]
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
        Assert.Same(config1, config2);
    }
}
