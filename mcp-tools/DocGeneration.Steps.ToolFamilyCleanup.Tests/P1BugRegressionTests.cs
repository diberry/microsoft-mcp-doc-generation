// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Regression tests for P1 bugs triaged in AD-018 (2026-03-23).
/// Tests the post-processing pipeline (FamilyFileStitcher) and individual services
/// to verify bug fixes and prevent regressions.
///
/// Bug #193 + #191: Annotations should be inline with blank line
/// Bug #192: Duplicate @mcpcli markers
/// Bug #190: Backticks on "for example" values
/// </summary>
public class P1BugRegressionTests
{
    // ── Bug #193 + #191: Annotations inline with blank line ─────────────

    [Fact]
    public void Stitch_InlineAnnotations_BlankLineInsertedByPostProcessing()
    {
        // Arrange — tool section with inline annotations MISSING blank line
        // (simulates template output that forgot the blank line)
        var familyContent = BuildFamilyContentSingleTool(
            "storage", "Account list", "storage account list",
            BuildToolSectionWithAnnotation(
                "Account list", "storage account list",
                "Lists all storage accounts in the subscription.",
                annotationLinkAndValues:
                    "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):\n" +
                    "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌"));

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(familyContent);

        // Assert — AnnotationSpaceFixer (step 7 in pipeline) should insert blank line
        Assert.Contains("server):\n\nDestructive:", result);
        Assert.DoesNotContain("server):\nDestructive:", result);
    }

    [Fact]
    public void Stitch_InlineAnnotations_AlreadyHasBlankLine_PreservedIdempotently()
    {
        // Arrange — correct format: blank line already present
        var familyContent = BuildFamilyContentSingleTool(
            "keyvault", "Secret list", "keyvault secret list",
            BuildToolSectionWithAnnotation(
                "Secret list", "keyvault secret list",
                "Lists secrets in Azure Key Vault.",
                annotationLinkAndValues:
                    "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):\n\n" +
                    "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ✅ | Local Required: ❌"));

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(familyContent);

        // Assert — no triple newline introduced (idempotent)
        Assert.Contains("server):\n\nDestructive:", result);
        Assert.DoesNotContain("server):\n\n\nDestructive:", result);
    }

    [Fact]
    public void Stitch_MultipleToolsWithInlineAnnotations_AllGetBlankLine()
    {
        // Arrange — three tools across diverse Azure services, each missing blank line
        var tools = new List<ToolContent>
        {
            BuildToolContent("storage", "Account list", "storage account list",
                BuildToolSectionWithAnnotation("Account list", "storage account list",
                    "Lists storage accounts.",
                    "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):\nDestructive: ❌ | Idempotent: ✅ | Read Only: ✅")),
            BuildToolContent("storage", "Container create", "storage container create",
                BuildToolSectionWithAnnotation("Container create", "storage container create",
                    "Creates a storage container.",
                    "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):\nDestructive: ❌ | Idempotent: ❌ | Read Only: ❌")),
            BuildToolContent("storage", "Blob delete", "storage blob delete",
                BuildToolSectionWithAnnotation("Blob delete", "storage blob delete",
                    "Deletes a blob from Azure Storage.",
                    "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):\nDestructive: ✅ | Idempotent: ✅ | Read Only: ❌"))
        };

        var familyContent = new FamilyContent
        {
            FamilyName = "storage",
            Metadata = "---\ntitle: Azure Storage tools\ntool_count: 3\n---\n# Azure MCP (Model Context Protocol) Server tools for Azure Storage\n\nManage Azure Storage resources.",
            Tools = tools,
            RelatedContent = "## Related content\n- [Azure Storage docs](https://learn.microsoft.com/azure/storage)"
        };

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(familyContent);

        // Assert — all three should have blank line between link and values
        var annotationLinks = Regex.Matches(result, @"\[Tool annotation hints\]\([^\)]+\):");
        Assert.Equal(3, annotationLinks.Count);
        // No annotation link directly followed by content on next line
        Assert.DoesNotContain("server):\nDestructive:", result);
    }

    [Fact]
    public void Stitch_InlineAnnotationOutput_DoesNotContainIncludeDirective()
    {
        // Arrange — tool sections with inline annotation content (post-fix format)
        var familyContent = BuildFamilyContentSingleTool(
            "cosmosdb", "Account list", "cosmosdb account list",
            BuildToolSectionWithAnnotation(
                "Account list", "cosmosdb account list",
                "Lists Azure Cosmos DB accounts.",
                annotationLinkAndValues:
                    "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):\n\n" +
                    "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌"));

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(familyContent);

        // Assert — output should NOT contain [!INCLUDE] for annotations
        Assert.DoesNotContain("[!INCLUDE", result);
        // Assert — output SHOULD contain inline annotation values
        Assert.Contains("Destructive: ❌", result);
        Assert.Contains("Idempotent: ✅", result);
        Assert.Contains("Read Only: ✅", result);
    }

    // ── Bug #192: Duplicate @mcpcli markers ─────────────────────────────

    [Fact]
    public void Stitch_ToolSectionsWithSingleMarkers_OutputHasExactlyOnePerTool()
    {
        // Arrange — three tools, each with exactly one @mcpcli marker
        var tools = new List<ToolContent>
        {
            BuildToolContent("aks", "Cluster list", "aks cluster list",
                "## Cluster list\n<!-- @mcpcli aks cluster list -->\n\nLists AKS clusters."),
            BuildToolContent("aks", "Cluster get", "aks cluster get",
                "## Cluster get\n<!-- @mcpcli aks cluster get -->\n\nGets AKS cluster details."),
            BuildToolContent("aks", "Node pool list", "aks nodepool list",
                "## Node pool list\n<!-- @mcpcli aks nodepool list -->\n\nLists node pools.")
        };

        var familyContent = new FamilyContent
        {
            FamilyName = "aks",
            Metadata = "---\ntitle: AKS tools\ntool_count: 3\n---\n# Azure MCP (Model Context Protocol) Server tools for AKS\n\nManage Azure Kubernetes Service.",
            Tools = tools,
            RelatedContent = "## Related content\n- [AKS docs](https://learn.microsoft.com/azure/aks)"
        };

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(familyContent);

        // Assert — exactly 3 markers total (one per tool)
        var markers = Regex.Matches(result, @"<!-- @mcpcli .+? -->");
        Assert.Equal(3, markers.Count);

        // Each unique command appears exactly once
        var commands = markers.Select(m => m.Value).ToList();
        Assert.Equal(commands.Count, commands.Distinct().Count());
    }

    [Fact]
    public void Stitch_MarkerAppearsImmediatelyAfterH2()
    {
        // Arrange — tool with marker right after H2 heading
        var toolContent = "## Secret get\n<!-- @mcpcli keyvault secret get -->\n\nGets a secret from Key Vault.";
        var familyContent = BuildFamilyContentSingleTool(
            "keyvault", "Secret get", "keyvault secret get", toolContent);

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(familyContent);

        // Assert — marker appears after H2 heading
        var h2Index = result.IndexOf("## Secret get");
        var markerIndex = result.IndexOf("<!-- @mcpcli keyvault secret get -->");
        Assert.True(h2Index >= 0, "H2 heading should exist");
        Assert.True(markerIndex >= 0, "Marker should exist");
        Assert.True(markerIndex > h2Index, "Marker should appear after H2 heading");

        // No content between H2 and marker except whitespace
        var between = result.Substring(h2Index + "## Secret get".Length, markerIndex - h2Index - "## Secret get".Length);
        Assert.True(string.IsNullOrWhiteSpace(between),
            $"Only whitespace should be between H2 and marker, got: '{between}'");
    }

    [Fact]
    public void Stitch_DuplicateMarkersInInput_NotAddedByStitcher()
    {
        // Arrange — tool content that already has duplicate markers (upstream bug)
        var toolContent = string.Join("\n",
            "## Monitor metric list",
            "<!-- @mcpcli monitor metric list -->",
            "<!-- @mcpcli monitor metric list -->",
            "",
            "Lists available metrics for a resource.");

        var familyContent = BuildFamilyContentSingleTool(
            "monitor", "Monitor metric list", "monitor metric list", toolContent);

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(familyContent);

        // Assert — stitcher should not ADD more markers
        // (Input had 2, output should have at most 2 — stitcher shouldn't make it worse)
        var markerCount = Regex.Matches(result, @"<!-- @mcpcli monitor metric list -->").Count;
        Assert.True(markerCount <= 2,
            $"Stitcher should not introduce additional markers (found {markerCount})");
    }

    [Theory]
    [InlineData("advisor", "advisor recommendation list")]
    [InlineData("appservice", "appservice webapp list")]
    [InlineData("sql", "sql database list")]
    [InlineData("monitor", "monitor metric list")]
    [InlineData("speech", "speech recognizer list")]
    public void Stitch_VariousServices_ExactlyOneMarkerPerTool(string family, string command)
    {
        // Arrange — single tool, verify exactly one marker in output
        var toolContent = $"## {command.Split(' ').Last()}\n<!-- @mcpcli {command} -->\n\nTool description.";
        var familyContent = BuildFamilyContentSingleTool(family, command, command, toolContent);

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(familyContent);

        // Assert — exactly one marker
        var markerCount = Regex.Matches(result, $@"<!-- @mcpcli {Regex.Escape(command)} -->").Count;
        Assert.Equal(1, markerCount);
    }

    // ── Bug #190: Backticks on "for example" values ─────────────────────

    [Fact]
    public void Stitch_BareExampleValues_WrappedInBackticksByPipeline()
    {
        // Arrange — parameter table with bare (for example, ...) values
        var toolContent = string.Join("\n",
            "## Database add",
            "<!-- @mcpcli appservice database add -->",
            "",
            "Adds a database connection to your app.",
            "",
            "| Parameter | Required or optional | Description |",
            "|-----------|---------------------|-------------|",
            "| **Server** | Required | FQDN of the database server (for example, myserver.database.windows.net). |",
            "| **Database** | Required | Name of the database (for example, mydb). |",
            "",
            "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):",
            "",
            "Destructive: ❌ | Idempotent: ✅");

        var familyContent = BuildFamilyContentSingleTool(
            "appservice", "Database add", "appservice database add", toolContent);

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(familyContent);

        // Assert — bare values wrapped in backticks by ExampleValueBackticker (step 9)
        Assert.Contains("(for example, `myserver.database.windows.net`)", result);
        Assert.Contains("(for example, `mydb`)", result);
        Assert.DoesNotContain("(for example, myserver.database.windows.net)", result);
        Assert.DoesNotContain("(for example, mydb)", result);
    }

    [Fact]
    public void Stitch_AlreadyBacktickedValues_NotDoubleWrapped()
    {
        // Arrange — parameter table with already-backticked values
        var toolContent = string.Join("\n",
            "## Metric list",
            "<!-- @mcpcli monitor metric list -->",
            "",
            "Lists metrics for a resource.",
            "",
            "| Parameter | Required or optional | Description |",
            "|-----------|---------------------|-------------|",
            "| **Interval** | Optional | Time interval (for example, `PT1H` for 1 hour, `PT5M` for 5 minutes). |",
            "",
            "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):",
            "",
            "Destructive: ❌ | Idempotent: ✅");

        var familyContent = BuildFamilyContentSingleTool(
            "monitor", "Metric list", "monitor metric list", toolContent);

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(familyContent);

        // Assert — already-backticked values remain unchanged (idempotent)
        Assert.Contains("(for example, `PT1H` for 1 hour, `PT5M` for 5 minutes)", result);
        Assert.DoesNotContain("``PT1H``", result);
    }

    [Fact]
    public void Stitch_CommaSeparatedExampleValues_EachWrapped()
    {
        // Arrange — parameter with comma-separated bare values
        var toolContent = string.Join("\n",
            "## Detector get",
            "<!-- @mcpcli appservice detector get -->",
            "",
            "Gets a diagnostic detector result.",
            "",
            "| Parameter | Required or optional | Description |",
            "|-----------|---------------------|-------------|",
            "| **Detector** | Required | The detector name (for example, Availability, CpuAnalysis, MemoryAnalysis). |");

        var familyContent = BuildFamilyContentSingleTool(
            "appservice", "Detector get", "appservice detector get", toolContent);

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(familyContent);

        // Assert — each comma-separated value wrapped individually
        Assert.Contains("(for example, `Availability`, `CpuAnalysis`, `MemoryAnalysis`)", result);
    }

    [Theory]
    [InlineData("appservice", "my-webapp")]
    [InlineData("cosmosdb", "my-cosmos-account")]
    [InlineData("sql", "myserver.database.windows.net")]
    [InlineData("aks", "my-cluster")]
    [InlineData("storage", "mystorageaccount")]
    public void Stitch_VariousServices_ExampleValuesWrapped(string service, string exampleValue)
    {
        var toolContent = string.Join("\n",
            $"## Resource get",
            $"<!-- @mcpcli {service} resource get -->",
            "",
            $"Gets {service} resource details.",
            "",
            "| Parameter | Required or optional | Description |",
            "|-----------|---------------------|-------------|",
            $"| **Name** | Required | The resource name (for example, {exampleValue}). |");

        var familyContent = BuildFamilyContentSingleTool(
            service, "Resource get", $"{service} resource get", toolContent);

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(familyContent);

        // Assert — example value wrapped in backticks
        Assert.Contains($"(for example, `{exampleValue}`)", result);
    }

    // ── Integration: All bug fixes work together ────────────────────────

    [Fact]
    public void Stitch_RealisticToolFamily_AllBugsPrevented()
    {
        // Arrange — realistic tool family page with multiple bug scenarios
        var tools = new List<ToolContent>
        {
            BuildToolContent("storage", "Account list", "storage account list",
                string.Join("\n",
                    "## Account list",
                    "<!-- @mcpcli storage account list -->",
                    "",
                    "Lists all storage accounts in the subscription.",
                    "",
                    "| Parameter | Required or optional | Description |",
                    "|-----------|---------------------|-------------|",
                    "| **Resource group** | Optional | Filter by resource group (for example, rg-prod). |",
                    "",
                    "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):",
                    "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌")),
            BuildToolContent("storage", "Account get", "storage account get",
                string.Join("\n",
                    "## Account get",
                    "<!-- @mcpcli storage account get -->",
                    "",
                    "Gets details for a storage account.",
                    "",
                    "| Parameter | Required or optional | Description |",
                    "|-----------|---------------------|-------------|",
                    "| **Account** | Required | Name of the storage account (for example, mystorageaccount). |",
                    "",
                    "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):",
                    "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌"))
        };

        var familyContent = new FamilyContent
        {
            FamilyName = "storage",
            Metadata = "---\ntitle: Azure Storage tools\ntool_count: 2\n---\n# Azure MCP (Model Context Protocol) Server tools for Azure Storage\n\nManage your Azure Storage resources.",
            Tools = tools,
            RelatedContent = "## Related content\n- [Azure Storage](https://learn.microsoft.com/azure/storage)"
        };

        var stitcher = new FamilyFileStitcher();

        // Act
        var result = stitcher.Stitch(familyContent);

        // Assert Bug #193: No [!INCLUDE] in annotation sections
        Assert.DoesNotContain("[!INCLUDE", result);

        // Assert Bug #191: Blank line between annotation link and values
        Assert.DoesNotContain("server):\nDestructive:", result);
        Assert.Contains("server):\n\nDestructive:", result);

        // Assert Bug #192: Exactly 2 @mcpcli markers (one per tool)
        var markerCount = Regex.Matches(result, @"<!-- @mcpcli .+? -->").Count;
        Assert.Equal(2, markerCount);

        // Assert Bug #190: Example values wrapped in backticks
        Assert.Contains("(for example, `rg-prod`)", result);
        Assert.Contains("(for example, `mystorageaccount`)", result);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static string BuildToolSectionWithAnnotation(
        string heading, string command, string description, string annotationLinkAndValues)
    {
        return string.Join("\n",
            $"## {heading}",
            $"<!-- @mcpcli {command} -->",
            "",
            description,
            "",
            annotationLinkAndValues);
    }

    private static ToolContent BuildToolContent(
        string family, string heading, string command, string content)
    {
        return new ToolContent
        {
            ToolName = heading.ToLowerInvariant(),
            FileName = $"azure-{family}-{heading.ToLowerInvariant().Replace(' ', '-')}.md",
            FamilyName = family,
            Content = content,
            Command = command
        };
    }

    private static FamilyContent BuildFamilyContentSingleTool(
        string family, string heading, string command, string content)
    {
        return new FamilyContent
        {
            FamilyName = family,
            Metadata = $"---\ntitle: {family} tools\ntool_count: 1\n---\n# Azure MCP (Model Context Protocol) Server tools for {family}\n\nOverview.",
            Tools = new List<ToolContent>
            {
                BuildToolContent(family, heading, command, content)
            },
            RelatedContent = $"## Related content\n- [{family} docs](https://learn.microsoft.com/azure/{family})"
        };
    }
}
