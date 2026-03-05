// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using RelatedSkillsGenerator.Models;
using Xunit;

namespace RelatedSkillsGenerator.Tests;

public class MappingLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public MappingLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"MappingLoaderTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    private string WriteMappingFile(string filename, object content)
    {
        var filePath = Path.Combine(_tempDir, filename);
        var json = JsonSerializer.Serialize(content, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
        return filePath;
    }

    [Fact]
    public async Task LoadMapping_ValidJson_ParsesCorrectly()
    {
        var mappings = new[]
        {
            new
            {
                McpNamespace = "storage",
                BrandName = "Azure Storage",
                Skills = new[]
                {
                    new { Name = "azure-storage", Relationship = "primary" },
                    new { Name = "azure-blob-storage", Relationship = "supplementary" }
                }
            },
            new
            {
                McpNamespace = "keyvault",
                BrandName = "Azure Key Vault",
                Skills = new[]
                {
                    new { Name = "azure-key-vault", Relationship = "primary" }
                }
            }
        };

        var filePath = WriteMappingFile("test-mapping.json", mappings);

        var result = await Program.LoadSkillMappingsAsync(filePath);

        Assert.Equal(2, result.Count);

        var storage = result.First(m => m.McpNamespace == "storage");
        Assert.Equal("Azure Storage", storage.BrandName);
        Assert.Equal(2, storage.Skills.Count);
        Assert.Equal("azure-storage", storage.Skills[0].Name);
        Assert.Equal("primary", storage.Skills[0].Relationship);

        var keyvault = result.First(m => m.McpNamespace == "keyvault");
        Assert.Single(keyvault.Skills);
    }

    [Fact]
    public async Task LoadMapping_EmptySkills_HandledCorrectly()
    {
        var mappings = new[]
        {
            new
            {
                McpNamespace = "monitor",
                BrandName = "Azure Monitor",
                Skills = Array.Empty<object>()
            }
        };

        var filePath = WriteMappingFile("empty-skills.json", mappings);

        var result = await Program.LoadSkillMappingsAsync(filePath);

        Assert.Single(result);
        Assert.Equal("monitor", result[0].McpNamespace);
        Assert.Empty(result[0].Skills);
    }

    [Fact]
    public async Task LoadMapping_OtherCategory_Present()
    {
        var mappings = new[]
        {
            new
            {
                McpNamespace = "cosmos",
                BrandName = "Azure Cosmos DB",
                Skills = new[]
                {
                    new { Name = "azure-cosmos-db", Relationship = "primary" },
                    new { Name = "azure-cosmos-db-nosql", Relationship = "other" }
                }
            }
        };

        var filePath = WriteMappingFile("other-category.json", mappings);

        var result = await Program.LoadSkillMappingsAsync(filePath);

        Assert.Single(result);
        var cosmos = result[0];
        Assert.Equal(2, cosmos.Skills.Count);

        var otherSkill = cosmos.Skills.First(s => s.Relationship == "other");
        Assert.Equal("azure-cosmos-db-nosql", otherSkill.Name);
    }

    [Fact]
    public async Task LoadMapping_FileNotFound_ReturnsEmptyList()
    {
        var filePath = Path.Combine(_tempDir, "nonexistent.json");

        var result = await Program.LoadSkillMappingsAsync(filePath);

        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadMapping_InvalidJson_ReturnsEmptyList()
    {
        var filePath = Path.Combine(_tempDir, "invalid.json");
        await File.WriteAllTextAsync(filePath, "{ this is not valid json }");

        var result = await Program.LoadSkillMappingsAsync(filePath);

        Assert.Empty(result);
    }
}
