// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Integration tests exercising the full pipeline:
/// ToolReader reads files → ordering applied → FamilyFileStitcher produces output.
/// Covers real heading formats, verb mapping, non-contiguous resource types,
/// duplicate-heading disambiguation, and edge cases (#505).
/// </summary>
public class ToolReaderToStitcherIntegrationTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FamilyFileStitcher _stitcher;

    public ToolReaderToStitcherIntegrationTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"integration-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _stitcher = new FamilyFileStitcher();
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    #region Helpers

    /// <summary>
    /// Creates a tool .md file in the temp directory using the format ToolReader expects.
    /// </summary>
    private void CreateToolFile(string fileName, string @namespace, string resourceAndVerb, string heading, string body)
    {
        var command = $"{@namespace} {resourceAndVerb}";
        var content = $"""
            # {heading}

            <!-- @mcpcli {command} -->

            {body}
            """;

        File.WriteAllText(Path.Combine(_tempDir, fileName), content);
    }

    /// <summary>
    /// Reads tools from the temp directory using ToolReader, then stitches
    /// via FamilyFileStitcher for a specific family, producing the final markdown.
    /// </summary>
    private async Task<string> RunFullPipeline(string familyName)
    {
        var reader = new ToolReader(_tempDir);
        var toolsByFamily = await reader.ReadAndGroupToolsAsync();

        Assert.True(toolsByFamily.ContainsKey(familyName),
            $"Family '{familyName}' not found. Available: {string.Join(", ", toolsByFamily.Keys)}");

        var tools = toolsByFamily[familyName];

        var familyContent = new FamilyContent
        {
            FamilyName = familyName,
            Metadata = $"---\ntitle: {familyName}\n---\n\n# {familyName}\n\nOverview paragraph.",
            Tools = tools,
            RelatedContent = "## Related content\n\n- [Link](https://example.com)"
        };

        return _stitcher.Stitch(familyContent);
    }

    /// <summary>
    /// Reads tools from the temp directory using ToolReader and returns the ordered tool list
    /// for a specific family (without stitching).
    /// </summary>
    private async Task<List<ToolContent>> ReadToolsForFamily(string familyName)
    {
        var reader = new ToolReader(_tempDir);
        var toolsByFamily = await reader.ReadAndGroupToolsAsync();

        Assert.True(toolsByFamily.ContainsKey(familyName),
            $"Family '{familyName}' not found. Available: {string.Join(", ", toolsByFamily.Keys)}");

        return toolsByFamily[familyName];
    }

    #endregion

    #region End-to-End Ordering Tests

    [Fact]
    public async Task FullPipeline_SingleResource_OrdersByToolNameAlphabetically()
    {
        // Arrange: create tools with realistic headings in non-alphabetical filename order
        CreateToolFile("advisor-recommendation-list.md", "advisor", "recommendation list",
            "List recommendations", "Lists all advisor recommendations.");
        CreateToolFile("advisor-recommendation-get.md", "advisor", "recommendation get",
            "Get recommendation", "Gets a specific recommendation.");
        CreateToolFile("advisor-recommendation-disable.md", "advisor", "recommendation disable",
            "Disable recommendation", "Disables a recommendation.");

        // Act
        var result = await RunFullPipeline("advisor");

        // Assert: single-resource family → stitcher sorts by ToolName alphabetically
        // ToolReader parses H1 as ToolName: "Disable recommendation", "Get recommendation", "List recommendations"
        var disablePos = result.IndexOf("Disable recommendation");
        var getPos = result.IndexOf("Get recommendation");
        var listPos = result.IndexOf("List recommendations");

        Assert.True(disablePos > 0, "Disable heading should be present");
        Assert.True(getPos > 0, "Get heading should be present");
        Assert.True(listPos > 0, "List heading should be present");

        Assert.True(disablePos < getPos, "Disable should appear before Get (alphabetical)");
        Assert.True(getPos < listPos, "Get should appear before List (alphabetical)");
    }

    [Fact]
    public async Task FullPipeline_VerbMappingHeadings_OrderByDisplayHeadingNotCommand()
    {
        // Arrange: verbs in commands differ from display headings
        // "list" verb but heading says "Show all..." — ordering should follow the ToolName (H1)
        CreateToolFile("storage-account-list.md", "storage", "account list",
            "Show all storage accounts", "Shows all storage accounts in a subscription.");
        CreateToolFile("storage-account-create.md", "storage", "account create",
            "Create a storage account", "Creates a new storage account.");
        CreateToolFile("storage-account-delete.md", "storage", "account delete",
            "Remove a storage account", "Removes a storage account.");

        // Act
        var result = await RunFullPipeline("storage");

        // Assert: stitcher orders by ToolName (the H1 heading), not by command verb
        // "Create a storage account" < "Remove a storage account" < "Show all storage accounts"
        var createPos = result.IndexOf("Create a storage account");
        var removePos = result.IndexOf("Remove a storage account");
        var showPos = result.IndexOf("Show all storage accounts");

        Assert.True(createPos > 0 && removePos > 0 && showPos > 0, "All headings should be present");
        Assert.True(createPos < removePos, "Create should appear before Remove");
        Assert.True(removePos < showPos, "Remove should appear before Show");
    }

    #endregion

    #region Multi-Resource Non-Contiguous Tests

    [Fact]
    public async Task FullPipeline_MultiResource_NonContiguousInput_GroupsCorrectly()
    {
        // Arrange: vm and disk tools mixed together (non-contiguous resource types)
        // ToolReader sorts by resource sort key, grouping them regardless of file order
        CreateToolFile("compute-vm-create.md", "compute", "vm create",
            "Create virtual machine", "## Create virtual machine\n\nCreates a VM.");
        CreateToolFile("compute-disk-list.md", "compute", "disk list",
            "List disks", "## List disks\n\nLists all disks.");
        CreateToolFile("compute-vm-delete.md", "compute", "vm delete",
            "Delete virtual machine", "## Delete virtual machine\n\nDeletes a VM.");
        CreateToolFile("compute-disk-create.md", "compute", "disk create",
            "Create disk", "## Create disk\n\nCreates a disk.");
        CreateToolFile("compute-vm-list.md", "compute", "vm list",
            "List virtual machines", "## List virtual machines\n\nLists all VMs.");

        // Act: read through ToolReader (which groups by resource sort key)
        var tools = await ReadToolsForFamily("compute");

        // Assert: ToolReader groups all disk tools together and all vm tools together
        var commands = tools.Select(t => t.Command).ToList();

        // disk tools should be contiguous
        var diskIndices = commands.Select((c, i) => (c, i))
            .Where(x => x.c!.Contains("disk"))
            .Select(x => x.i)
            .ToList();
        Assert.True(diskIndices.SequenceEqual(Enumerable.Range(diskIndices[0], diskIndices.Count)),
            "Disk tools should be grouped contiguously after ToolReader ordering");

        // vm tools should be contiguous
        var vmIndices = commands.Select((c, i) => (c, i))
            .Where(x => x.c!.Contains(" vm "))
            .Select(x => x.i)
            .ToList();
        Assert.True(vmIndices.SequenceEqual(Enumerable.Range(vmIndices[0], vmIndices.Count)),
            "VM tools should be grouped contiguously after ToolReader ordering");
    }

    [Fact]
    public async Task FullPipeline_MultiResource_StitcherProducesResourceGroupHeaders()
    {
        // Arrange: multi-resource family
        CreateToolFile("compute-vm-create.md", "compute", "vm create",
            "Create virtual machine", "## Create virtual machine\n\nCreates a VM.");
        CreateToolFile("compute-disk-list.md", "compute", "disk list",
            "List disks", "## List disks\n\nLists all disks.");

        // Act
        var result = await RunFullPipeline("compute");

        // Assert: stitcher emits H2 resource group headers
        Assert.Contains("## VM", result);
        Assert.Contains("## Disk", result);
    }

    #endregion

    #region Duplicate Heading Disambiguation

    [Fact]
    public async Task FullPipeline_DuplicateToolNames_BothAppearInOutput()
    {
        // Arrange: two tools with the same H1 heading (would produce same ToolName)
        CreateToolFile("keyvault-secret-list.md", "keyvault", "secret list",
            "List secrets", "## List secrets\n\nLists all secrets in the vault.");
        CreateToolFile("keyvault-certificate-list.md", "keyvault", "certificate list",
            "List secrets", "## List secrets\n\nLists all certificates as secrets.");

        // Act
        var tools = await ReadToolsForFamily("keyvault");

        // Assert: both tools are present (ToolReader doesn't deduplicate)
        Assert.Equal(2, tools.Count);
        Assert.All(tools, t => Assert.Equal("List secrets", t.ToolName));

        // Assert: they are disambiguated by FileName in the stitcher's tie-break
        var familyContent = new FamilyContent
        {
            FamilyName = "keyvault",
            Metadata = "---\ntitle: keyvault\n---\n\n# keyvault\n\nOverview.",
            Tools = tools,
            RelatedContent = "## Related content"
        };

        var result = _stitcher.Stitch(familyContent);

        // Both should appear in output — ordered by filename tie-break
        // "keyvault-certificate-list.md" < "keyvault-secret-list.md"
        var certContent = result.IndexOf("Lists all certificates as secrets");
        var secretContent = result.IndexOf("Lists all secrets in the vault");
        Assert.True(certContent > 0, "Certificate tool content should be present");
        Assert.True(secretContent > 0, "Secret tool content should be present");
        Assert.True(certContent < secretContent,
            "Certificate file (earlier filename) should appear before secret file");
    }

    #endregion

    #region ToolReader Ordering Determines Input to Stitcher

    [Fact]
    public async Task ToolReader_OrderDeterminesStitcherInput_NotResidualSort()
    {
        // Arrange: create tools where ToolReader's resource-first sort differs
        // from simple alphabetical sort by ToolName
        CreateToolFile("storage-blob-get.md", "storage", "blob get",
            "Get blob", "## Get blob\n\nGets a blob.");
        CreateToolFile("storage-account-create.md", "storage", "account create",
            "Create account", "## Create account\n\nCreates an account.");
        CreateToolFile("storage-blob-create.md", "storage", "blob create",
            "Create blob", "## Create blob\n\nCreates a blob.");
        CreateToolFile("storage-account-list.md", "storage", "account list",
            "List accounts", "## List accounts\n\nLists accounts.");

        // Act: read tools via ToolReader
        var tools = await ReadToolsForFamily("storage");

        // Assert: ToolReader sorts by resource sort key (account before blob)
        var commands = tools.Select(t => t.Command).ToList();
        var accountCommands = commands.Where(c => c!.Contains("account")).ToList();
        var blobCommands = commands.Where(c => c!.Contains("blob")).ToList();

        var firstAccountIndex = commands.IndexOf(accountCommands.First());
        var firstBlobIndex = commands.IndexOf(blobCommands.First());

        Assert.True(firstAccountIndex < firstBlobIndex,
            "ToolReader should sort 'account' resource group before 'blob' resource group");

        // Within each resource group, verify alphabetical by tool name
        Assert.Equal("storage account create", commands[firstAccountIndex]);
        Assert.Equal("storage account list", commands[firstAccountIndex + 1]);
    }

    #endregion

    #region Empty Family Edge Case

    [Fact]
    public async Task FullPipeline_EmptyDirectory_ReturnsEmptyFamilyDictionary()
    {
        // Arrange: temp directory has no .md files (already empty from constructor)

        // Act
        var reader = new ToolReader(_tempDir);
        var toolsByFamily = await reader.ReadAndGroupToolsAsync();

        // Assert
        Assert.Empty(toolsByFamily);
    }

    [Fact]
    public void Stitcher_EmptyToolList_ProducesOutputWithoutToolSections()
    {
        // Arrange
        var familyContent = new FamilyContent
        {
            FamilyName = "empty",
            Metadata = "---\ntitle: empty\n---\n\n# Empty family\n\nNo tools here.",
            Tools = new List<ToolContent>(),
            RelatedContent = "## Related content"
        };

        // Act
        var result = _stitcher.Stitch(familyContent);

        // Assert: metadata and related content present, no tool sections
        Assert.Contains("# Empty family", result);
        Assert.Contains("## Related content", result);
        Assert.DoesNotContain("## Create", result);
        Assert.DoesNotContain("## List", result);
    }

    #endregion

    #region Single-Resource Family End-to-End

    [Fact]
    public async Task FullPipeline_SingleResourceFamily_EndToEnd_ProducesCorrectOutput()
    {
        // Arrange: a realistic single-resource family (all same resource type → single-resource path)
        CreateToolFile("advisor-recommendation-list.md", "advisor", "recommendation list",
            "List recommendations",
            "## List recommendations\n\nRetrieves all recommendations.\n\n### Parameters\n\n| Name | Description |\n|------|-------------|\n| filter | Filter expression |");
        CreateToolFile("advisor-recommendation-get.md", "advisor", "recommendation get",
            "Get recommendation",
            "## Get recommendation\n\nRetrieves a single recommendation.\n\n### Parameters\n\n| Name | Description |\n|------|-------------|\n| id | Recommendation ID |");
        CreateToolFile("advisor-recommendation-dismiss.md", "advisor", "recommendation dismiss",
            "Dismiss recommendation",
            "## Dismiss recommendation\n\nDismisses a recommendation.\n\n### Parameters\n\n| Name | Description |\n|------|-------------|\n| id | Recommendation ID |");

        // Act
        var result = await RunFullPipeline("advisor");

        // Assert: correct structure
        Assert.Contains("# advisor", result);
        Assert.Contains("Overview paragraph", result);
        Assert.Contains("## Related content", result);

        // Assert: all three tools present in alphabetical order by ToolName
        var dismissPos = result.IndexOf("Dismiss recommendation");
        var getPos = result.IndexOf("Get recommendation");
        var listPos = result.IndexOf("List recommendations");

        Assert.True(dismissPos > 0 && getPos > 0 && listPos > 0, "All tools present");
        Assert.True(dismissPos < getPos, "Dismiss before Get");
        Assert.True(getPos < listPos, "Get before List");

        // Assert: parameter tables are preserved
        Assert.Contains("| filter | Filter expression |", result);
        Assert.Contains("| id | Recommendation ID |", result);
    }

    #endregion

    #region Ordering Stability

    [Fact]
    public async Task FullPipeline_RepeatedRuns_ProduceIdenticalOutput()
    {
        // Arrange
        CreateToolFile("monitor-alert-create.md", "monitor", "alert create",
            "Create alert", "## Create alert\n\nCreates an alert rule.");
        CreateToolFile("monitor-alert-list.md", "monitor", "alert list",
            "List alerts", "## List alerts\n\nLists all alerts.");
        CreateToolFile("monitor-alert-delete.md", "monitor", "alert delete",
            "Delete alert", "## Delete alert\n\nDeletes an alert.");

        // Act: run pipeline multiple times
        var result1 = await RunFullPipeline("monitor");
        var result2 = await RunFullPipeline("monitor");
        var result3 = await RunFullPipeline("monitor");

        // Assert: deterministic output
        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
    }

    #endregion
}
