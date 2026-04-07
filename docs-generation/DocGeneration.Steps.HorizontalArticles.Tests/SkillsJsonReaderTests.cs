// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using HorizontalArticleGenerator.Services;
using Xunit;

namespace HorizontalArticleGenerator.Tests;

public class SkillsJsonReaderTests : IDisposable
{
    private readonly string _outputBasePath;

    public SkillsJsonReaderTests()
    {
        _outputBasePath = Path.Combine(Path.GetTempPath(), "skills-reader-tests", Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputBasePath))
            Directory.Delete(_outputBasePath, recursive: true);
    }

    [Fact]
    public async Task LoadSkillsAsync_MissingFile_ReturnsEmptyList()
    {
        var result = await SkillsJsonReader.LoadSkillsAsync(_outputBasePath, "nonexistent");
        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadSkillsAsync_MalformedJson_ReturnsEmptyList()
    {
        var dir = Path.Combine(_outputBasePath, "skills-relevance");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "storage-skills-relevance.json"), "not valid json {{{");

        var result = await SkillsJsonReader.LoadSkillsAsync(_outputBasePath, "storage");
        Assert.Empty(result);
    }

    [Fact]
    public async Task LoadSkillsAsync_ValidJson_ReturnsFilteredSkills()
    {
        await WriteSkillsJsonAsync("compute", new[]
        {
            new { name = "High Skill", source = "test", sourceUrl = "https://example.com/high", relevanceScore = 0.9, relevanceLevel = "High", description = "A high relevance skill", tags = new[] { "azure" } },
            new { name = "Medium Skill", source = "test", sourceUrl = "https://example.com/med", relevanceScore = 0.5, relevanceLevel = "Medium", description = "A medium relevance skill", tags = new[] { "azure" } },
            new { name = "Low Skill", source = "test", sourceUrl = "https://example.com/low", relevanceScore = 0.3, relevanceLevel = "Low", description = "A low relevance skill", tags = new[] { "azure" } }
        });

        var result = await SkillsJsonReader.LoadSkillsAsync(_outputBasePath, "compute");

        Assert.Equal(2, result.Count);
        Assert.Equal("High Skill", result[0].Name);
        Assert.Equal("Medium Skill", result[1].Name);
    }

    [Fact]
    public async Task LoadSkillsAsync_FiltersAtThreshold_IncludesExactly05()
    {
        await WriteSkillsJsonAsync("advisor", new[]
        {
            new { name = "Exact Threshold", source = "test", sourceUrl = "https://example.com", relevanceScore = 0.5, relevanceLevel = "Medium", description = "Exactly at threshold", tags = new[] { "azure" } },
            new { name = "Below Threshold", source = "test", sourceUrl = "https://example.com", relevanceScore = 0.49, relevanceLevel = "Low", description = "Just below", tags = new[] { "azure" } }
        });

        var result = await SkillsJsonReader.LoadSkillsAsync(_outputBasePath, "advisor");

        Assert.Single(result);
        Assert.Equal("Exact Threshold", result[0].Name);
    }

    [Fact]
    public async Task LoadSkillsAsync_OrdersByScoreDescending()
    {
        await WriteSkillsJsonAsync("search", new[]
        {
            new { name = "Medium", source = "test", sourceUrl = "https://example.com/m", relevanceScore = 0.6, relevanceLevel = "Medium", description = "Medium skill", tags = new[] { "azure" } },
            new { name = "Highest", source = "test", sourceUrl = "https://example.com/h", relevanceScore = 0.95, relevanceLevel = "High", description = "Highest skill", tags = new[] { "azure" } },
            new { name = "High", source = "test", sourceUrl = "https://example.com/hi", relevanceScore = 0.8, relevanceLevel = "High", description = "High skill", tags = new[] { "azure" } }
        });

        var result = await SkillsJsonReader.LoadSkillsAsync(_outputBasePath, "search");

        Assert.Equal(3, result.Count);
        Assert.Equal("Highest", result[0].Name);
        Assert.Equal("High", result[1].Name);
        Assert.Equal("Medium", result[2].Name);
    }

    [Fact]
    public async Task LoadSkillsAsync_CustomMinScore()
    {
        await WriteSkillsJsonAsync("redis", new[]
        {
            new { name = "High", source = "test", sourceUrl = "https://example.com", relevanceScore = 0.9, relevanceLevel = "High", description = "High skill", tags = new[] { "azure" } },
            new { name = "Medium", source = "test", sourceUrl = "https://example.com", relevanceScore = 0.6, relevanceLevel = "Medium", description = "Medium skill", tags = new[] { "azure" } }
        });

        var result = await SkillsJsonReader.LoadSkillsAsync(_outputBasePath, "redis", minScore: 0.8);

        Assert.Single(result);
        Assert.Equal("High", result[0].Name);
    }

    [Fact]
    public async Task LoadSkillsAsync_SanitizesDescription_EscapesPipes()
    {
        await WriteSkillsJsonAsync("cosmos", new[]
        {
            new { name = "Pipe Skill", source = "test", sourceUrl = "https://example.com", relevanceScore = 0.8, relevanceLevel = "High", description = "Uses pipes | in text", tags = new[] { "azure" } }
        });

        var result = await SkillsJsonReader.LoadSkillsAsync(_outputBasePath, "cosmos");

        Assert.Single(result);
        Assert.Equal(@"Uses pipes \| in text", result[0].Description);
    }

    [Fact]
    public async Task LoadSkillsAsync_SanitizesDescription_RemovesNewlines()
    {
        await WriteSkillsJsonAsync("functions", new[]
        {
            new { name = "Newline Skill", source = "test", sourceUrl = "https://example.com", relevanceScore = 0.8, relevanceLevel = "High", description = "Line1\nLine2\r\nLine3", tags = new[] { "azure" } }
        });

        var result = await SkillsJsonReader.LoadSkillsAsync(_outputBasePath, "functions");

        Assert.Single(result);
        Assert.Equal("Line1 Line2 Line3", result[0].Description);
    }

    [Fact]
    public async Task LoadSkillsAsync_EmptySkillsArray_ReturnsEmptyList()
    {
        var dir = Path.Combine(_outputBasePath, "skills-relevance");
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(new { serviceName = "test", generatedAt = "2025-01-01", skills = Array.Empty<object>() });
        await File.WriteAllTextAsync(Path.Combine(dir, "test-skills-relevance.json"), json);

        var result = await SkillsJsonReader.LoadSkillsAsync(_outputBasePath, "test");
        Assert.Empty(result);
    }

    [Theory]
    [InlineData("storage", "storage")]
    [InlineData("Cosmos DB", "cosmos-db")]
    [InlineData("KEY vault", "key-vault")]
    public void SanitizeFileName_NormalizesCorrectly(string input, string expected)
    {
        Assert.Equal(expected, SkillsJsonReader.SanitizeFileName(input));
    }

    [Theory]
    [InlineData("simple text", "simple text")]
    [InlineData("has | pipe", @"has \| pipe")]
    [InlineData("has\nnewline", "has newline")]
    [InlineData("has\r\ncrlf", "has crlf")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SanitizeForTable_HandlesEdgeCases(string? input, string expected)
    {
        Assert.Equal(expected, SkillsJsonReader.SanitizeForTable(input!));
    }

    private async Task WriteSkillsJsonAsync(string serviceId, object skills)
    {
        var dir = Path.Combine(_outputBasePath, "skills-relevance");
        Directory.CreateDirectory(dir);
        var data = new { serviceName = serviceId, generatedAt = DateTime.UtcNow.ToString("o"), skills };
        var json = JsonSerializer.Serialize(data);
        await File.WriteAllTextAsync(Path.Combine(dir, $"{serviceId}-skills-relevance.json"), json);
    }
}
