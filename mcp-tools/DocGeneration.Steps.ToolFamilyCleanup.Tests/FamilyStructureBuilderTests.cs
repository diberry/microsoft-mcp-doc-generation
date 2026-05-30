using System.Text.Json;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

public sealed class FamilyStructureBuilderTests : IDisposable
{
    private readonly string _testRoot;

    public FamilyStructureBuilderTests()
    {
        _testRoot = Path.Combine(AppContext.BaseDirectory, "family-structure-builder-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public async Task BuildAsync_ExtractsSectionsInCanonicalOrder()
    {
        var toolsDirectory = Path.Combine(_testRoot, "tools");
        Directory.CreateDirectory(toolsDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(toolsDirectory, "compute-disk-delete.md"),
            """
            ---
            ---
            # Delete disk

            <!-- @mcpcli compute disk delete -->

            Removes a disk.
            """);
        await File.WriteAllTextAsync(
            Path.Combine(toolsDirectory, "compute-disk-create.md"),
            """
            ---
            ---
            # Create disk

            <!-- @mcpcli compute disk create -->

            Creates a disk.
            """);

        var builder = new FamilyStructureBuilder();

        var result = await builder.BuildAsync(toolsDirectory, "compute", h2HeadingsDirectory: null, CancellationToken.None);

        Assert.Equal("compute", result.FamilyName);
        Assert.Equal(2, result.Sections.Count);
        Assert.Equal("Create disk", result.Sections[0].Heading);
        Assert.Equal(["compute disk create"], result.Sections[0].ToolNames);
        Assert.StartsWith("## Create disk", result.Sections[0].SourceContent.ReplaceLineEndings());
        Assert.Equal("Delete disk", result.Sections[1].Heading);
        Assert.Equal(["compute disk delete"], result.Sections[1].ToolNames);
    }

    [Fact]
    public async Task BuildAsync_EmptyDirectory_ReturnsEmptySections()
    {
        var toolsDirectory = Path.Combine(_testRoot, "empty-tools");
        Directory.CreateDirectory(toolsDirectory);

        var builder = new FamilyStructureBuilder();

        var result = await builder.BuildAsync(toolsDirectory, "compute", h2HeadingsDirectory: null, CancellationToken.None);

        Assert.Equal("compute", result.FamilyName);
        Assert.Empty(result.Sections);
    }

    [Fact]
    public async Task BuildAsync_SchemaVersion_IsAlwaysOnePointZero()
    {
        var toolsDirectory = Path.Combine(_testRoot, "schema-tools");
        Directory.CreateDirectory(toolsDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(toolsDirectory, "compute-disk-create.md"),
            """
            # Create disk
            <!-- @mcpcli compute disk create -->
            Creates a disk.
            """);

        var builder = new FamilyStructureBuilder();

        var result = await builder.BuildAsync(toolsDirectory, "compute", h2HeadingsDirectory: null, CancellationToken.None);

        Assert.Equal("1.0", result.SchemaVersion);
    }

    [Fact]
    public async Task BuildAsync_LoadsHeadingsFromJson_WhenAvailable()
    {
        var toolsDirectory = Path.Combine(_testRoot, "json-tools");
        var h2HeadingsDirectory = Path.Combine(_testRoot, "h2-headings");
        Directory.CreateDirectory(toolsDirectory);
        Directory.CreateDirectory(h2HeadingsDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(toolsDirectory, "compute-disk-create.md"),
            """
            ---
            ---
            # Create disk

            <!-- @mcpcli compute disk create -->

            Creates a disk.
            """);
        await File.WriteAllTextAsync(
            Path.Combine(h2HeadingsDirectory, "compute.json"),
            JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["compute disk create"] = "Provision disk"
            }));

        var builder = new FamilyStructureBuilder();

        var result = await builder.BuildAsync(toolsDirectory, "compute", h2HeadingsDirectory, CancellationToken.None);

        Assert.Single(result.Sections);
        Assert.Equal("Provision disk", result.Sections[0].Heading);
        Assert.StartsWith("## Provision disk", result.Sections[0].SourceContent.ReplaceLineEndings());
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
