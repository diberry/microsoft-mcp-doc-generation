using System.Text.Json;
using Shared;
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

    [Fact]
    public async Task BuildAsync_FilesForDifferentFamily_AreExcludedFromSections()
    {
        var toolsDirectory = Path.Combine(_testRoot, "mixed-tools");
        Directory.CreateDirectory(toolsDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(toolsDirectory, "compute-vm-create.md"),
            """
            ---
            ---
            # Create VM

            <!-- @mcpcli compute vm create -->

            Creates a VM.
            """);

        // This file belongs to the 'storage' family — must not appear when building 'compute'.
        await File.WriteAllTextAsync(
            Path.Combine(toolsDirectory, "storage-account-list.md"),
            """
            ---
            ---
            # List accounts

            <!-- @mcpcli storage account list -->

            Lists accounts.
            """);

        var builder = new FamilyStructureBuilder();

        var result = await builder.BuildAsync(toolsDirectory, "compute", h2HeadingsDirectory: null, CancellationToken.None);

        Assert.Equal("compute", result.FamilyName);
        Assert.Single(result.Sections);
        Assert.Equal("Create virtual machine", result.Sections[0].Heading);
        Assert.DoesNotContain(result.Sections, s => s.Heading == "List accounts");
    }

    [Fact]
    public async Task BuildAsync_NonExistentDirectory_ReturnsEmptySections()
    {
        var toolsDirectory = Path.Combine(_testRoot, "does-not-exist");

        var builder = new FamilyStructureBuilder();

        var result = await builder.BuildAsync(toolsDirectory, "compute", h2HeadingsDirectory: null, CancellationToken.None);

        Assert.Equal("compute", result.FamilyName);
        Assert.Empty(result.Sections);
    }

    [Fact]
    public async Task BuildAsync_EmptyFamilyGroup_ReturnsEmptySections_WhenNoFileMatchesFamily()
    {
        // The directory contains tool files, but none belong to the requested family.
        var toolsDirectory = Path.Combine(_testRoot, "no-match-tools");
        Directory.CreateDirectory(toolsDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(toolsDirectory, "storage-account-list.md"),
            """
            ---
            ---
            # List accounts

            <!-- @mcpcli storage account list -->

            Lists accounts.
            """);
        await File.WriteAllTextAsync(
            Path.Combine(toolsDirectory, "keyvault-secret-get.md"),
            """
            ---
            ---
            # Get secret

            <!-- @mcpcli keyvault secret get -->

            Gets a secret.
            """);

        var builder = new FamilyStructureBuilder();

        var result = await builder.BuildAsync(toolsDirectory, "cosmos", h2HeadingsDirectory: null, CancellationToken.None);

        Assert.Equal("cosmos", result.FamilyName);
        Assert.Empty(result.Sections);
    }

    [Fact]
    public async Task BuildAsync_SingleToolNamespace_ProducesSingleSection()
    {
        var toolsDirectory = Path.Combine(_testRoot, "single-tool");
        Directory.CreateDirectory(toolsDirectory);

        await File.WriteAllTextAsync(
            Path.Combine(toolsDirectory, "monitor-metrics-query.md"),
            """
            ---
            ---
            # Query metrics

            <!-- @mcpcli monitor metrics query -->

            Queries metrics.
            """);

        var builder = new FamilyStructureBuilder();

        var result = await builder.BuildAsync(toolsDirectory, "monitor", h2HeadingsDirectory: null, CancellationToken.None);

        Assert.Equal("monitor", result.FamilyName);
        Assert.Single(result.Sections);
        Assert.Equal(["monitor metrics query"], result.Sections[0].ToolNames);
        Assert.StartsWith("## ", result.Sections[0].SourceContent.ReplaceLineEndings());
    }

    [Fact]
    public async Task BuildAsync_StripsPhantomParametersNotPresentInManifest()
    {
        var toolsDirectory = Path.Combine(_testRoot, "phantom-tools");
        var parametersDirectory = Path.Combine(_testRoot, "parameters");
        Directory.CreateDirectory(toolsDirectory);
        Directory.CreateDirectory(parametersDirectory);

        const string command = "cosmos account show";
        await File.WriteAllTextAsync(
            Path.Combine(toolsDirectory, "cosmos-account-show.md"),
            """
            ---
            ---
            # Show account

            <!-- @mcpcli cosmos account show -->

            Shows an Azure Cosmos DB account.

            | Parameter | Required or optional | Description |
            | --- | --- | --- |
            | `account` | Required | Account name. |
            | `authentication-method` | Optional | Authentication mode. |
            """);

        var nameContext = await FileNameContext.CreateAsync();
        var manifestPath = Path.Combine(
            parametersDirectory,
            ToolFileNameBuilder.BuildParameterManifestFileName(command, nameContext));
        await File.WriteAllTextAsync(
            manifestPath,
            """
            {
              "schemaVersion": "2.0",
              "toolCommand": "cosmos account show",
              "namespace": "cosmos",
              "sourceIdentity": { "azureMcpBuild": "1.0.0", "generatedAtUtc": "2026-01-01T00:00:00Z" },
              "parameters": [
                {
                  "canonicalName": "account",
                  "displayName": "account",
                  "displayAliases": ["account"],
                  "placeholderAliases": ["account"],
                  "required": true,
                  "requiredText": "Required",
                  "isConditionalRequired": false,
                  "description": "Account name."
                }
              ]
            }
            """);

        var builder = new FamilyStructureBuilder();

        var result = await builder.BuildAsync(toolsDirectory, "cosmos", h2HeadingsDirectory: null, CancellationToken.None);

        var section = Assert.Single(result.Sections);
        Assert.Contains("`account`", section.SourceContent, StringComparison.Ordinal);
        Assert.DoesNotContain("authentication-method", section.SourceContent, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
