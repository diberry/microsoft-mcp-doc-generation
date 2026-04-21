using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SkillsGen.Core.Data;
using SkillsGen.Core.Generation;
using SkillsGen.Core.Models;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Data;

public class CuratedDataLoaderTests
{
    private static readonly ILogger<CuratedDataLoader> Logger =
        NullLogger<CuratedDataLoader>.Instance;

    /// <summary>
    /// Finds the data/ directory relative to the test output directory.
    /// Mirrors the pattern used in SkillInventoryLoaderTests.
    /// Returns null if not found (so tests can skip gracefully in CI).
    /// </summary>
    private static string? FindDataDirectory()
    {
        var path = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data"));
        return Directory.Exists(path) ? path : null;
    }

    // ── Test 1 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_WithValidFiles_ReturnsCuratedData()
    {
        var dataPath = FindDataDirectory();
        if (dataPath is null) return; // skip gracefully if data/ absent

        var result = CuratedDataLoader.Load(dataPath, Logger);

        result.Should().NotBeEmpty();
        result.Should().ContainKey("azure-storage");
        result["azure-storage"].RelatedLinks.Should().NotBeEmpty();
        result["azure-storage"].ExamplePrompts.Should().NotBeEmpty();
    }

    // ── Test 2 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_WithMissingFiles_ReturnsEmptyDictionary()
    {
        var result = CuratedDataLoader.Load("nonexistent-path-xyz-does-not-exist", Logger);

        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    // ── Test 3 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_RelatedLinks_HasExpectedStructure()
    {
        var dataPath = FindDataDirectory();
        if (dataPath is null) return;

        var result = CuratedDataLoader.Load(dataPath, Logger);

        result.Should().ContainKey("azure-storage");
        var links = result["azure-storage"].RelatedLinks;
        links.Should().NotBeEmpty();

        var first = links[0];
        first.Title.Should().NotBeNullOrWhiteSpace();
        first.Url.Should().NotBeNullOrWhiteSpace();
        first.Category.Should().NotBeNullOrWhiteSpace();
    }

    // ── Test 4 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_ExamplePrompts_HasExpectedCount()
    {
        var dataPath = FindDataDirectory();
        if (dataPath is null) return;

        var result = CuratedDataLoader.Load(dataPath, Logger);

        result.Should().ContainKey("azure-storage");
        // azure-storage has exactly 7 prompts in the published cache
        result["azure-storage"].ExamplePrompts.Should().HaveCount(7);
    }

    // ── Test 5 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Load_MissingSkill_ReturnsEmptyLists()
    {
        var dataPath = FindDataDirectory();
        if (dataPath is null) return;

        var result = CuratedDataLoader.Load(dataPath, Logger);

        // A skill that definitely doesn't exist in the JSON files
        result.Should().NotContainKey("nonexistent-skill-xyz-123");

        // When the key is absent, a default CuratedSkillData has empty collections
        var skillData = result.GetValueOrDefault("nonexistent-skill-xyz-123")
                        ?? new CuratedSkillData(RelatedLinks: [], ExamplePrompts: []);

        skillData.RelatedLinks.Should().BeEmpty();
        skillData.ExamplePrompts.Should().BeEmpty();
    }
}

// ── Integration tests: generator uses curated data ───────────────────────────

public class GeneratorCuratedDataIntegrationTests
{
    private readonly ILogger<SkillPageGenerator> _logger =
        Substitute.For<ILogger<SkillPageGenerator>>();

    // Minimal template that renders the shouldTrigger list so we can assert on it
    private static readonly string TriggersTemplate = """
        ---
        title: Azure skill for {{displayName}}
        description: {{description}}
        ---
        # Azure skill for {{displayName}}

        {{#if hasTriggers}}
        ## Example prompts
        {{#each shouldTrigger}}
        - {{this}}
        {{/each}}
        {{/if}}
        """;

    private static SkillData CreateStorageSkillData() => new()
    {
        Name = "azure-storage",
        DisplayName = "Azure Storage",
        Description = "Manage Azure Storage accounts and resources.",
        UseFor = ["deploy storage", "create blob container"]
    };

    // ── Test 6 ───────────────────────────────────────────────────────────────

    [Fact]
    public void Generator_WithCuratedPrompts_PrefersCuratedOverFallback()
    {
        var curatedPrompts = new List<string>
        {
            "Upload a file to my Azure Blob Storage container",
            "Download a blob from my storage account",
            "List all containers in my Azure Storage account"
        };

        var curatedData = new Dictionary<string, CuratedSkillData>
        {
            ["azure-storage"] = new CuratedSkillData(RelatedLinks: [], ExamplePrompts: curatedPrompts)
        };

        // Empty ShouldTrigger forces the generator to choose between curated and fallback
        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var gen = new SkillPageGenerator(TriggersTemplate, _logger, curatedData);
        var result = gen.Generate(CreateStorageSkillData(), triggers, tier, prereqs);

        // Curated prompts must appear
        result.Should().Contain("Upload a file to my Azure Blob Storage container");
        result.Should().Contain("Download a blob from my storage account");

        // Generic "How do I" fallback must NOT appear
        result.Should().NotContain("How do I deploy storage");
        result.Should().NotContain("How do I");
    }

    // ── Test 7 ───────────────────────────────────────────────────────────────

    [Fact]
    public void Generator_WithoutCuratedPrompts_UsesFallback()
    {
        // No curated data provided — fallback logic should produce "How do I..." prompts
        var gen = new SkillPageGenerator(TriggersTemplate, _logger);

        var triggers = new TriggerData([], [], null);
        var tier = new TierAssessment(1, [], "Test", false, false, false, false, false);
        var prereqs = new SkillPrerequisites();

        var result = gen.Generate(CreateStorageSkillData(), triggers, tier, prereqs);

        // The fallback generator converts UseFor items into "How do I..." questions
        result.Should().Contain("How do I");
    }
}
