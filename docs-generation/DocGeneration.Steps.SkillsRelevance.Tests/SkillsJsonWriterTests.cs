// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using SkillsRelevance.Models;
using SkillsRelevance.Output;
using Xunit;

namespace DocGeneration.Steps.SkillsRelevance.Tests;

public class SkillsJsonWriterTests : IDisposable
{
    private readonly string _outputDir;

    public SkillsJsonWriterTests()
    {
        _outputDir = Path.Combine(Path.GetTempPath(), "skills-json-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_outputDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
    }

    [Fact]
    public async Task WriteServiceJsonAsync_CreatesJsonFile()
    {
        var skills = new List<SkillInfo>
        {
            CreateSkill("Azure Skill", 0.85, "GitHub Awesome Copilot", "https://github.com/test/skill1")
        };

        await SkillsMarkdownWriter.WriteServiceJsonAsync(_outputDir, "storage", skills);

        var filePath = Path.Combine(_outputDir, "storage-skills-relevance.json");
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task WriteServiceJsonAsync_JsonRoundtrips()
    {
        var skills = new List<SkillInfo>
        {
            CreateSkill("Copilot for Azure", 0.9, "Microsoft Skills", "https://github.com/ms/skill"),
            CreateSkill("Storage Helper", 0.6, "GitHub Awesome Copilot", "https://github.com/test/storage")
        };

        await SkillsMarkdownWriter.WriteServiceJsonAsync(_outputDir, "storage", skills);

        var filePath = Path.Combine(_outputDir, "storage-skills-relevance.json");
        var json = await File.ReadAllTextAsync(filePath);
        var deserialized = JsonSerializer.Deserialize<SkillsRelevanceJsonOutput>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserialized);
        Assert.Equal("storage", deserialized!.ServiceName);
        Assert.Equal(2, deserialized.Skills.Count);
        Assert.Equal("Copilot for Azure", deserialized.Skills[0].Name);
        Assert.Equal(0.9, deserialized.Skills[0].RelevanceScore);
        Assert.Equal("High", deserialized.Skills[0].RelevanceLevel);
        Assert.Equal("Storage Helper", deserialized.Skills[1].Name);
        Assert.Equal("Medium", deserialized.Skills[1].RelevanceLevel);
    }

    [Fact]
    public async Task WriteServiceJsonAsync_EmptySkills_WritesEmptyArray()
    {
        await SkillsMarkdownWriter.WriteServiceJsonAsync(_outputDir, "keyvault", new List<SkillInfo>());

        var filePath = Path.Combine(_outputDir, "keyvault-skills-relevance.json");
        var json = await File.ReadAllTextAsync(filePath);
        var deserialized = JsonSerializer.Deserialize<SkillsRelevanceJsonOutput>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserialized);
        Assert.Empty(deserialized!.Skills);
    }

    [Fact]
    public async Task WriteServiceJsonAsync_IncludesTagsAndDescription()
    {
        var skill = CreateSkill("Monitor Skill", 0.7, "Microsoft Skills", "https://github.com/ms/monitor");
        skill.Description = "Helps with Azure Monitor diagnostics";
        skill.Tags = new List<string> { "monitoring", "diagnostics", "azure" };

        await SkillsMarkdownWriter.WriteServiceJsonAsync(_outputDir, "monitor", new List<SkillInfo> { skill });

        var filePath = Path.Combine(_outputDir, "monitor-skills-relevance.json");
        var json = await File.ReadAllTextAsync(filePath);
        var deserialized = JsonSerializer.Deserialize<SkillsRelevanceJsonOutput>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserialized);
        var entry = Assert.Single(deserialized!.Skills);
        Assert.Equal("Helps with Azure Monitor diagnostics", entry.Description);
        Assert.Equal(3, entry.Tags.Count);
        Assert.Contains("monitoring", entry.Tags);
    }

    [Fact]
    public async Task WriteServiceJsonAsync_SanitizesFilename()
    {
        await SkillsMarkdownWriter.WriteServiceJsonAsync(_outputDir, "Cosmos DB", new List<SkillInfo>());

        var filePath = Path.Combine(_outputDir, "cosmos-db-skills-relevance.json");
        Assert.True(File.Exists(filePath));
    }

    [Theory]
    [InlineData(0.95, "High")]
    [InlineData(0.8, "High")]
    [InlineData(0.65, "Medium")]
    [InlineData(0.5, "Medium")]
    [InlineData(0.35, "Low")]
    [InlineData(0.2, "Low")]
    [InlineData(0.1, "Minimal")]
    [InlineData(0.0, "Minimal")]
    public void ScoreToLevel_ReturnsCorrectLevel(double score, string expected)
    {
        Assert.Equal(expected, SkillJsonEntry.ScoreToLevel(score));
    }

    [Fact]
    public void FromSkillInfo_MapsAllFields()
    {
        var skill = new SkillInfo
        {
            Name = "Test Skill",
            SourceRepository = "test/repo",
            SourceUrl = "https://github.com/test/repo/skill.md",
            RelevanceScore = 0.75,
            Description = "A test skill",
            Tags = new List<string> { "test", "azure" }
        };

        var entry = SkillJsonEntry.FromSkillInfo(skill);

        Assert.Equal("Test Skill", entry.Name);
        Assert.Equal("test/repo", entry.Source);
        Assert.Equal("https://github.com/test/repo/skill.md", entry.SourceUrl);
        Assert.Equal(0.75, entry.RelevanceScore);
        Assert.Equal("Medium", entry.RelevanceLevel);
        Assert.Equal("A test skill", entry.Description);
        Assert.Equal(2, entry.Tags.Count);
    }

    [Fact]
    public async Task WriteServiceJsonAsync_HasValidGeneratedAt()
    {
        var before = DateTime.UtcNow;
        await SkillsMarkdownWriter.WriteServiceJsonAsync(_outputDir, "redis", new List<SkillInfo>());
        var after = DateTime.UtcNow;

        var filePath = Path.Combine(_outputDir, "redis-skills-relevance.json");
        var json = await File.ReadAllTextAsync(filePath);
        var deserialized = JsonSerializer.Deserialize<SkillsRelevanceJsonOutput>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserialized);
        Assert.True(DateTimeOffset.TryParse(deserialized!.GeneratedAt, out var parsed));
        Assert.True(parsed >= before.AddSeconds(-1) && parsed <= after.AddSeconds(1));
    }

    private static SkillInfo CreateSkill(string name, double score, string source, string url)
    {
        return new SkillInfo
        {
            Name = name,
            RelevanceScore = score,
            SourceRepository = source,
            SourceUrl = url,
            Description = $"Description for {name}",
            Tags = new List<string> { "azure" }
        };
    }
}
