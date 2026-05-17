using FluentAssertions;
using SkillsGen.Core.Cataloging;
using Xunit;

namespace SkillsGen.Core.Tests.Integration;

/// <summary>
/// Integration test: fixture SKILL.md → catalog → warnings.
/// Verifies the full cataloging flow end-to-end using a realistic SKILL.md fixture.
/// </summary>
public class SourceOutlineCatalogIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly SourceOutlineCataloger _cataloger;
    private readonly SourceOutlineWriter _writer;
    private readonly List<string> _warnings;

    public SourceOutlineCatalogIntegrationTests()
    {
        _tempDir = Path.Combine(AppContext.BaseDirectory, "test-integration-catalog", Guid.NewGuid().ToString("N"));
        _cataloger = new SourceOutlineCataloger();
        _writer = new SourceOutlineWriter(_tempDir);
        _warnings = [];
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private static string LoadFixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        return File.ReadAllText(path);
    }

    [Fact]
    public void FullFlow_FixtureSkill_CatalogsAndWritesAndEmitsWarnings()
    {
        var content = LoadFixture("catalog-fixture-skill.md");
        const string skillName = "azure-keyvault";

        // Step 1: catalog
        var outline = _cataloger.Catalog(skillName, content);

        // Verify known headings are mapped
        outline.Headings.Should().Contain(h => h.Text == "Use cases" && h.MappedTo == "When to use this skill");
        outline.Headings.Should().Contain(h => h.Text == "Negative use cases" && h.MappedTo == "When not to use this skill");
        outline.Headings.Should().Contain(h => h.Text == "Azure services" && h.MappedTo == "Azure services knowledge");
        outline.Headings.Should().Contain(h => h.Text == "Prerequisites" && h.MappedTo == "Prerequisites");
        outline.Headings.Should().Contain(h => h.Text == "RBAC" && h.MappedTo == "Prerequisites (RBAC sub-section)");
        outline.Headings.Should().Contain(h => h.Text == "Related Skills" && h.MappedTo == "Related skills");
        outline.Headings.Should().Contain(h => h.Text == "Decision Guidance" && h.MappedTo == "Decision guidance");

        // Excluded headings: MCP Tools, Workflow
        outline.Headings.Should().Contain(h => h.Text == "MCP Tools" && h.MappedTo == null);
        outline.Headings.Should().Contain(h => h.Text == "Workflow" && h.MappedTo == null);

        // Unknown heading
        outline.Headings.Should().Contain(h => h.Text == "New Section Without Mapping" && h.MappedTo == null);

        // Step 2: collect warnings
        foreach (var heading in outline.Headings.Where(h => !SkillsGen.Core.Cataloging.HeadingMappingRules.IsKnown(h.Text)))
        {
            _warnings.Add($"SKILL.md '{skillName}' has heading '{heading.Text}' with no mapping rule");
        }

        _warnings.Should().Contain(w => w.Contains("New Section Without Mapping"));
        _warnings.Should().Contain(w => w.Contains("Key vs Secret"));
        _warnings.Should().HaveCount(2); // "Key vs Secret" and "New Section Without Mapping"

        // UnmappedCount matches only truly unknown (not excluded) headings
        outline.UnmappedCount.Should().Be(2);

        // Step 3: persist
        var catalog = new Dictionary<string, SkillsGen.Core.Models.SkillOutline> { [skillName] = outline };
        _writer.Write(catalog);

        var outputPath = Path.Combine(_tempDir, "source-outlines.json");
        File.Exists(outputPath).Should().BeTrue();

        var json = File.ReadAllText(outputPath);
        json.Should().Contain("azure-keyvault");
        json.Should().Contain("New Section Without Mapping");
    }
}
