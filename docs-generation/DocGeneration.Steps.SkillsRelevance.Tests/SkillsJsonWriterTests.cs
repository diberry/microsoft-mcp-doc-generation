// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using Xunit;
using SkillsRelevance.Models;
using SkillsRelevance.Output;

namespace SkillsRelevance.Tests;

public class SkillsJsonWriterTests : IDisposable
{
    private readonly string _tempDir;

    public SkillsJsonWriterTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "skills-json-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── File creation ────────────────────────────────────────────────────

    [Fact]
    public async Task WriteServiceSummaryJsonAsync_CreatesJsonFile()
    {
        var skills = new List<SkillInfo>
        {
            CreateSkill("Azure Storage Helper", score: 0.85)
        };
        var sources = CreateSources();

        await SkillsJsonWriter.WriteServiceSummaryJsonAsync(
            _tempDir, "storage", skills, sources);

        var jsonPath = Path.Combine(_tempDir, "storage-skills-relevance.json");
        Assert.True(File.Exists(jsonPath), "JSON file should be created");
    }

    [Fact]
    public async Task WriteServiceSummaryJsonAsync_JsonFileCreatedAlongsideMarkdown()
    {
        var skills = new List<SkillInfo>
        {
            CreateSkill("Azure Storage Helper", score: 0.85)
        };
        var sources = CreateSources();

        await SkillsMarkdownWriter.WriteServiceSummaryAsync(
            _tempDir, "storage", skills, sources);
        await SkillsJsonWriter.WriteServiceSummaryJsonAsync(
            _tempDir, "storage", skills, sources);

        var mdPath = Path.Combine(_tempDir, "storage-skills-relevance.md");
        var jsonPath = Path.Combine(_tempDir, "storage-skills-relevance.json");
        Assert.True(File.Exists(mdPath), "Markdown file should exist");
        Assert.True(File.Exists(jsonPath), "JSON file should exist alongside markdown");
    }

    // ── Valid JSON structure ─────────────────────────────────────────────

    [Fact]
    public async Task WriteServiceSummaryJsonAsync_ProducesValidJson()
    {
        var skills = new List<SkillInfo>
        {
            CreateSkill("Azure Key Vault Manager", score: 0.72)
        };
        var sources = CreateSources();

        await SkillsJsonWriter.WriteServiceSummaryJsonAsync(
            _tempDir, "keyvault", skills, sources);

        var json = await File.ReadAllTextAsync(
            Path.Combine(_tempDir, "keyvault-skills-relevance.json"));
        var doc = JsonSerializer.Deserialize<SkillRelevanceOutput>(json);

        Assert.NotNull(doc);
        Assert.Equal("keyvault", doc!.Service);
        Assert.Single(doc.Skills);
    }

    [Fact]
    public async Task WriteServiceSummaryJsonAsync_SkillDataMatchesInput()
    {
        var timestamp = new DateTimeOffset(2025, 6, 15, 10, 30, 0, TimeSpan.Zero);
        var skill = CreateSkill("Cosmos DB Query Helper", score: 0.65);
        skill.SourceRepository = "Microsoft Skills";
        skill.SourceUrl = "https://github.com/microsoft/skills/cosmos.md";
        skill.FileName = "cosmos.md";
        skill.Description = "Helps with Cosmos DB NoSQL queries";
        skill.Tags = new List<string> { "cosmosdb", "nosql" };
        skill.LastUpdated = new DateTimeOffset(2025, 3, 10, 0, 0, 0, TimeSpan.Zero);
        skill.RelevanceReasons = new List<string> { "Name contains 'cosmos'" };

        await SkillsJsonWriter.WriteServiceSummaryJsonAsync(
            _tempDir, "cosmosdb", new List<SkillInfo> { skill },
            CreateSources(), timestamp);

        var json = await File.ReadAllTextAsync(
            Path.Combine(_tempDir, "cosmosdb-skills-relevance.json"));
        var doc = JsonSerializer.Deserialize<SkillRelevanceOutput>(json)!;

        Assert.Equal("cosmosdb", doc.Service);
        Assert.Equal("2025-06-15T10:30:00Z", doc.GeneratedAt);

        var entry = Assert.Single(doc.Skills);
        Assert.Equal("Cosmos DB Query Helper", entry.Name);
        Assert.Equal("Microsoft Skills", entry.Source);
        Assert.Equal("https://github.com/microsoft/skills/cosmos.md", entry.SourceUrl);
        Assert.Equal("cosmos.md", entry.FileName);
        Assert.Equal(0.65, entry.RelevanceScore);
        Assert.Equal("medium", entry.RelevanceLevel);
        Assert.Equal("Helps with Cosmos DB NoSQL queries", entry.Description);
        Assert.Equal(new List<string> { "cosmosdb", "nosql" }, entry.Tags);
        Assert.Equal("2025-03-10T00:00:00Z", entry.LastUpdated);
        Assert.Contains("Name contains 'cosmos'", entry.RelevanceReasons);
    }

    // ── Sources ──────────────────────────────────────────────────────────

    [Fact]
    public async Task WriteServiceSummaryJsonAsync_IncludesSources()
    {
        var sources = new List<SkillSource>
        {
            new() { Owner = "github", Repo = "awesome-copilot", Path = "skills", DisplayName = "GitHub Awesome Copilot" },
            new() { Owner = "microsoft", Repo = "skills", Path = "", DisplayName = "Microsoft Skills" }
        };

        await SkillsJsonWriter.WriteServiceSummaryJsonAsync(
            _tempDir, "monitor", new List<SkillInfo>(), sources);

        var json = await File.ReadAllTextAsync(
            Path.Combine(_tempDir, "monitor-skills-relevance.json"));
        var doc = JsonSerializer.Deserialize<SkillRelevanceOutput>(json)!;

        Assert.Equal(2, doc.Sources.Count);
        Assert.Equal("GitHub Awesome Copilot", doc.Sources[0].Name);
        Assert.Contains("github/awesome-copilot", doc.Sources[0].Url);
        Assert.Equal("Microsoft Skills", doc.Sources[1].Name);
    }

    // ── Empty skills ─────────────────────────────────────────────────────

    [Fact]
    public async Task WriteServiceSummaryJsonAsync_EmptySkillsList_ProducesValidJson()
    {
        await SkillsJsonWriter.WriteServiceSummaryJsonAsync(
            _tempDir, "functions", new List<SkillInfo>(), CreateSources());

        var json = await File.ReadAllTextAsync(
            Path.Combine(_tempDir, "functions-skills-relevance.json"));
        var doc = JsonSerializer.Deserialize<SkillRelevanceOutput>(json)!;

        Assert.Equal("functions", doc.Service);
        Assert.NotNull(doc.Skills);
        Assert.Empty(doc.Skills);
        Assert.NotEmpty(doc.Sources);
    }

    // ── Default/empty field handling ─────────────────────────────────────

    [Fact]
    public async Task WriteServiceSummaryJsonAsync_DefaultFields_HandledGracefully()
    {
        // SkillInfo with all defaults (empty strings, empty lists, null LastUpdated)
        var skill = new SkillInfo { RelevanceScore = 0.3 };

        await SkillsJsonWriter.WriteServiceSummaryJsonAsync(
            _tempDir, "redis", new List<SkillInfo> { skill }, CreateSources());

        var json = await File.ReadAllTextAsync(
            Path.Combine(_tempDir, "redis-skills-relevance.json"));
        var doc = JsonSerializer.Deserialize<SkillRelevanceOutput>(json)!;

        var entry = Assert.Single(doc.Skills);
        Assert.Equal(string.Empty, entry.Name);
        Assert.Equal(string.Empty, entry.Description);
        Assert.Empty(entry.Tags);
        Assert.Null(entry.LastUpdated);
        Assert.Empty(entry.RelevanceReasons);
        Assert.Equal("low", entry.RelevanceLevel);
    }

    // ── Relevance level thresholds ───────────────────────────────────────

    [Theory]
    [InlineData(0.95, "high")]
    [InlineData(0.80, "high")]
    [InlineData(0.79, "medium")]
    [InlineData(0.50, "medium")]
    [InlineData(0.49, "low")]
    [InlineData(0.20, "low")]
    [InlineData(0.19, "minimal")]
    [InlineData(0.0, "minimal")]
    public async Task WriteServiceSummaryJsonAsync_RelevanceLevelMappingIsCorrect(
        double score, string expectedLevel)
    {
        var skill = CreateSkill("Test Skill", score: score);

        await SkillsJsonWriter.WriteServiceSummaryJsonAsync(
            _tempDir, "test", new List<SkillInfo> { skill }, CreateSources());

        var json = await File.ReadAllTextAsync(
            Path.Combine(_tempDir, "test-skills-relevance.json"));
        var doc = JsonSerializer.Deserialize<SkillRelevanceOutput>(json)!;

        Assert.Equal(expectedLevel, doc.Skills[0].RelevanceLevel);
    }

    // ── Timestamp injection ──────────────────────────────────────────────

    [Fact]
    public async Task WriteServiceSummaryJsonAsync_UsesInjectedTimestamp()
    {
        var fixedTime = new DateTimeOffset(2025, 3, 16, 12, 34, 56, TimeSpan.Zero);

        await SkillsJsonWriter.WriteServiceSummaryJsonAsync(
            _tempDir, "search", new List<SkillInfo>(), CreateSources(), fixedTime);

        var json = await File.ReadAllTextAsync(
            Path.Combine(_tempDir, "search-skills-relevance.json"));
        var doc = JsonSerializer.Deserialize<SkillRelevanceOutput>(json)!;

        Assert.Equal("2025-03-16T12:34:56Z", doc.GeneratedAt);
    }

    // ── Multiple skills sorted by score ──────────────────────────────────

    [Fact]
    public async Task WriteServiceSummaryJsonAsync_PreservesSkillOrder()
    {
        var skills = new List<SkillInfo>
        {
            CreateSkill("High Relevance Skill", score: 0.9),
            CreateSkill("Medium Relevance Skill", score: 0.6),
            CreateSkill("Low Relevance Skill", score: 0.25)
        };

        await SkillsJsonWriter.WriteServiceSummaryJsonAsync(
            _tempDir, "storage", skills, CreateSources());

        var json = await File.ReadAllTextAsync(
            Path.Combine(_tempDir, "storage-skills-relevance.json"));
        var doc = JsonSerializer.Deserialize<SkillRelevanceOutput>(json)!;

        Assert.Equal(3, doc.Skills.Count);
        Assert.Equal("High Relevance Skill", doc.Skills[0].Name);
        Assert.Equal("Medium Relevance Skill", doc.Skills[1].Name);
        Assert.Equal("Low Relevance Skill", doc.Skills[2].Name);
    }

    // ── Filename sanitization ────────────────────────────────────────────

    [Fact]
    public async Task WriteServiceSummaryJsonAsync_SanitizesServiceNameInFilename()
    {
        await SkillsJsonWriter.WriteServiceSummaryJsonAsync(
            _tempDir, "Azure Key Vault", new List<SkillInfo>(), CreateSources());

        var expectedFile = Path.Combine(_tempDir, "azure-key-vault-skills-relevance.json");
        Assert.True(File.Exists(expectedFile));
    }

    // ── Score rounding ───────────────────────────────────────────────────

    [Fact]
    public async Task WriteServiceSummaryJsonAsync_RoundsScoreToTwoDecimals()
    {
        var skill = CreateSkill("Precision Skill", score: 0.8567);

        await SkillsJsonWriter.WriteServiceSummaryJsonAsync(
            _tempDir, "test", new List<SkillInfo> { skill }, CreateSources());

        var json = await File.ReadAllTextAsync(
            Path.Combine(_tempDir, "test-skills-relevance.json"));
        var doc = JsonSerializer.Deserialize<SkillRelevanceOutput>(json)!;

        Assert.Equal(0.86, doc.Skills[0].RelevanceScore);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static SkillInfo CreateSkill(string name, double score) => new()
    {
        Name = name,
        FileName = $"{name.ToLowerInvariant().Replace(' ', '-')}.md",
        SourceRepository = "Test Source",
        SourceUrl = $"https://github.com/test/{name.ToLowerInvariant().Replace(' ', '-')}.md",
        Description = $"Description for {name}",
        Tags = new List<string> { "azure", "test" },
        RelevanceScore = score,
        RelevanceReasons = new List<string> { $"Matched for {name}" }
    };

    private static List<SkillSource> CreateSources() => new()
    {
        new() { Owner = "microsoft", Repo = "skills", Path = "", DisplayName = "Microsoft Skills" }
    };
}
