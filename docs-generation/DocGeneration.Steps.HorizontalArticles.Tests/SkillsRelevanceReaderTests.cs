// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using HorizontalArticleGenerator.Generators;
using HorizontalArticleGenerator.Models;
using Xunit;

namespace HorizontalArticleGenerator.Tests;

public class SkillsRelevanceReaderTests : IDisposable
{
    private readonly string _outputBasePath;
    private readonly string _skillsDir;

    public SkillsRelevanceReaderTests()
    {
        _outputBasePath = Path.Combine(
            Path.GetTempPath(),
            "skills-reader-tests",
            Guid.NewGuid().ToString("N"));
        _skillsDir = Path.Combine(_outputBasePath, "skills-relevance");
        Directory.CreateDirectory(_skillsDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputBasePath))
            Directory.Delete(_outputBasePath, recursive: true);
    }

    // ── Loading and filtering ────────────────────────────────────────────

    [Fact]
    public void LoadRelevantSkills_WithHighRelevanceSkills_ReturnsFilteredSkills()
    {
        var json = CreateSkillsJson("storage", new[]
        {
            CreateEntry("Azure Storage Explorer", "Manage blobs and queues", 0.85, "high"),
            CreateEntry("Storage Metrics Viewer", "View storage metrics", 0.6, "medium"),
            CreateEntry("Low Relevance Skill", "Not very relevant", 0.15, "minimal")
        });
        WriteSkillsJson("storage", json);

        var result = SkillsRelevanceReader.LoadRelevantSkills(_outputBasePath, "storage");

        Assert.Equal(2, result.Count);
        Assert.Equal("Azure Storage Explorer", result[0].Name);
        Assert.Equal("Storage Metrics Viewer", result[1].Name);
    }

    [Fact]
    public void LoadRelevantSkills_WithScoreAboveThreshold_IncludesLowLevelSkills()
    {
        // Score > 0.3 qualifies even if relevanceLevel is "low"
        var json = CreateSkillsJson("keyvault", new[]
        {
            CreateEntry("Key Vault Helper", "Manage secrets", 0.35, "low")
        });
        WriteSkillsJson("keyvault", json);

        var result = SkillsRelevanceReader.LoadRelevantSkills(_outputBasePath, "keyvault");

        Assert.Single(result);
        Assert.Equal("Key Vault Helper", result[0].Name);
    }

    [Fact]
    public void LoadRelevantSkills_WithMediumLevel_IncludesEvenIfScoreLow()
    {
        // relevanceLevel "medium" qualifies regardless of score
        var json = CreateSkillsJson("cosmos", new[]
        {
            CreateEntry("Cosmos Helper", "Query helper", 0.1, "medium")
        });
        WriteSkillsJson("cosmos", json);

        var result = SkillsRelevanceReader.LoadRelevantSkills(_outputBasePath, "cosmos");

        Assert.Single(result);
    }

    // ── File not found ───────────────────────────────────────────────────

    [Fact]
    public void LoadRelevantSkills_NoJsonFile_ReturnsEmptyList()
    {
        var result = SkillsRelevanceReader.LoadRelevantSkills(_outputBasePath, "nonexistent");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── All skills below threshold ───────────────────────────────────────

    [Fact]
    public void LoadRelevantSkills_AllBelowThreshold_ReturnsEmptyList()
    {
        var json = CreateSkillsJson("functions", new[]
        {
            CreateEntry("Barely Relevant", "Some description", 0.15, "minimal"),
            CreateEntry("Also Low", "Another description", 0.25, "low")
        });
        WriteSkillsJson("functions", json);

        var result = SkillsRelevanceReader.LoadRelevantSkills(_outputBasePath, "functions");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── Invalid JSON ─────────────────────────────────────────────────────

    [Fact]
    public void LoadRelevantSkills_InvalidJson_ReturnsEmptyListWithoutThrowing()
    {
        File.WriteAllText(
            Path.Combine(_skillsDir, "badservice-skills-relevance.json"),
            "this is not valid json {{{");

        var result = SkillsRelevanceReader.LoadRelevantSkills(_outputBasePath, "badservice");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── Empty skills array ───────────────────────────────────────────────

    [Fact]
    public void LoadRelevantSkills_EmptySkillsArray_ReturnsEmptyList()
    {
        var json = CreateSkillsJson("monitor", Array.Empty<SkillRelevanceJsonEntry>());
        WriteSkillsJson("monitor", json);

        var result = SkillsRelevanceReader.LoadRelevantSkills(_outputBasePath, "monitor");

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // ── Description sanitization ─────────────────────────────────────────

    [Fact]
    public void LoadRelevantSkills_SanitizesDescriptions_EscapesPipesAndNewlines()
    {
        var json = CreateSkillsJson("redis", new[]
        {
            CreateEntry("Redis Helper", "Manage | cache\nand sessions", 0.9, "high")
        });
        WriteSkillsJson("redis", json);

        var result = SkillsRelevanceReader.LoadRelevantSkills(_outputBasePath, "redis");

        Assert.Single(result);
        Assert.Equal(@"Manage \| cache and sessions", result[0].Description);
    }

    // ── SourceUrl is passed through ──────────────────────────────────────

    [Fact]
    public void LoadRelevantSkills_PreservesSourceUrl()
    {
        var json = CreateSkillsJson("sql", new[]
        {
            CreateEntry("SQL Skill", "Desc", 0.8, "high", "https://github.com/microsoft/skills/sql.md")
        });
        WriteSkillsJson("sql", json);

        var result = SkillsRelevanceReader.LoadRelevantSkills(_outputBasePath, "sql");

        Assert.Single(result);
        Assert.Equal("https://github.com/microsoft/skills/sql.md", result[0].SourceUrl);
    }

    // ── IsRelevant predicate ─────────────────────────────────────────────

    [Theory]
    [InlineData(0.85, "high", true)]
    [InlineData(0.6, "medium", true)]
    [InlineData(0.35, "low", true)]       // score > 0.3
    [InlineData(0.31, "low", true)]       // score > 0.3
    [InlineData(0.3, "low", false)]       // score == 0.3, not > 0.3
    [InlineData(0.1, "medium", true)]     // level qualifies even if score low
    [InlineData(0.1, "high", true)]       // level qualifies even if score low
    [InlineData(0.25, "low", false)]      // neither qualifies
    [InlineData(0.15, "minimal", false)]  // neither qualifies
    public void IsRelevant_CorrectlyFilters(double score, string level, bool expected)
    {
        var entry = new SkillRelevanceJsonEntry
        {
            RelevanceScore = score,
            RelevanceLevel = level
        };

        Assert.Equal(expected, SkillsRelevanceReader.IsRelevant(entry));
    }

    // ── SanitizeForMarkdownTable ─────────────────────────────────────────

    [Theory]
    [InlineData("Simple text", "Simple text")]
    [InlineData("Has | pipe", @"Has \| pipe")]
    [InlineData("Has\nnewline", "Has newline")]
    [InlineData("Has\r\nCRLF", "Has CRLF")]
    [InlineData("  Leading spaces  ", "Leading spaces")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SanitizeForMarkdownTable_HandlesEdgeCases(string? input, string expected)
    {
        Assert.Equal(expected, SkillsRelevanceReader.SanitizeForMarkdownTable(input!));
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static SkillRelevanceJsonEntry CreateEntry(
        string name, string description, double score, string level, string? sourceUrl = null) =>
        new()
        {
            Name = name,
            Description = description,
            RelevanceScore = score,
            RelevanceLevel = level,
            SourceUrl = sourceUrl ?? $"https://github.com/test/{name.ToLowerInvariant().Replace(' ', '-')}"
        };

    private static string CreateSkillsJson(string service, IEnumerable<SkillRelevanceJsonEntry> skills)
    {
        var output = new SkillRelevanceJsonOutput
        {
            Service = service,
            Skills = skills.ToList()
        };
        return JsonSerializer.Serialize(output, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private void WriteSkillsJson(string serviceName, string json)
    {
        var filePath = Path.Combine(_skillsDir, $"{serviceName}-skills-relevance.json");
        File.WriteAllText(filePath, json);
    }
}
