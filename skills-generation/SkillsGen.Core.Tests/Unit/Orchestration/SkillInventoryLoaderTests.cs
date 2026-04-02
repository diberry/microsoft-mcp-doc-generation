using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SkillsGen.Core.Orchestration;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Orchestration;

public class SkillInventoryLoaderTests
{
    private readonly ILogger<SkillInventoryLoader> _logger = Substitute.For<ILogger<SkillInventoryLoader>>();

    [Fact]
    public void ParseJson_ValidJson_ReturnsEntries()
    {
        var loader = new SkillInventoryLoader(_logger);
        var json = """
        {
          "skills": [
            { "name": "azure-storage", "displayName": "Azure Storage", "category": "Data and Storage" },
            { "name": "azure-compute", "displayName": "Azure Compute", "category": "Infrastructure" }
          ]
        }
        """;

        var result = loader.ParseJson(json);

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("azure-storage");
        result[0].DisplayName.Should().Be("Azure Storage");
        result[0].Category.Should().Be("Data and Storage");
    }

    [Fact]
    public void ParseJson_InvalidJson_ReturnsEmptyList()
    {
        var loader = new SkillInventoryLoader(_logger);
        var result = loader.ParseJson("not json at all");

        result.Should().BeEmpty();
    }

    [Fact]
    public void Load_RealInventoryFile_ReturnsAllSkills()
    {
        var loader = new SkillInventoryLoader(_logger);
        var inventoryPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "skills-inventory.json");

        if (!File.Exists(inventoryPath))
        {
            // Try relative to the project
            inventoryPath = Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory, "..", "..", "..", "..", "data", "skills-inventory.json"));
        }

        // Skip if file not found (CI environment)
        if (!File.Exists(inventoryPath)) return;

        var result = loader.Load(inventoryPath);
        result.Should().NotBeEmpty();
        result.Should().Contain(e => e.Name == "azure-storage");
    }

    [Fact]
    public void Load_MissingFile_ReturnsEmptyList()
    {
        var loader = new SkillInventoryLoader(_logger);
        var result = loader.Load("nonexistent-path.json");

        result.Should().BeEmpty();
    }
}
