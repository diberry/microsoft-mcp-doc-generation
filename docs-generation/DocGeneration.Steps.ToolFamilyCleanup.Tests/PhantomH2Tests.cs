// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

public class PhantomH2Tests
{
    // ────────────────────────────────────────────────────────────────────────
    //  Helper: build realistic tool content blocks
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a realistic tool content block with H2 heading, description,
    /// parameter table, annotation marker, and example prompts.
    /// </summary>
    private static string BuildToolContent(
        string heading,
        string command,
        string description = "Manage resources in your Azure subscription.",
        bool includeParameterTable = true,
        bool includeAnnotation = true,
        bool includeExamplePrompts = true,
        string? extraH2Section = null,
        string? extraH2SectionAfterTable = null)
    {
        var lines = new List<string>
        {
            $"## {heading}",
            $"<!-- @mcpcli {command} -->",
            "",
            description,
            ""
        };

        if (extraH2Section != null)
        {
            lines.Add(extraH2Section);
        }

        if (includeExamplePrompts)
        {
            lines.Add("Example prompts include:");
            lines.Add("");
            lines.Add("- List all resources in the 'production' resource group.");
            lines.Add("- Show me the details of the 'my-resource' instance.");
            lines.Add("");
        }

        if (includeParameterTable)
        {
            lines.Add("| Parameter | Required | Description |");
            lines.Add("| --- | --- | --- |");
            lines.Add("| `resourceName` | Yes | The name of the resource. |");
            lines.Add("| `resourceGroup` | No | The resource group name. |");
            lines.Add("");
        }

        if (extraH2SectionAfterTable != null)
        {
            lines.Add(extraH2SectionAfterTable);
        }

        if (includeAnnotation)
        {
            lines.Add($"[Tool annotation: {command}]");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Builds a FamilyContent instance from a list of (toolName, content) tuples.
    /// </summary>
    private static FamilyContent BuildFamily(string familyName, params (string toolName, string content)[] tools)
    {
        return new FamilyContent
        {
            FamilyName = familyName,
            Metadata = $"---\ntitle: {familyName}\n---\n# {familyName}",
            RelatedContent = "## Related content\n- [Link](https://example.com)",
            Tools = tools.Select(t => new ToolContent
            {
                ToolName = t.toolName,
                FileName = $"azure-{familyName}-{t.toolName.Replace(' ', '-')}.complete.md",
                FamilyName = familyName,
                Content = t.content
            }).ToList()
        };
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ReplaceH2Heading Tests
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ReplaceH2Heading_SingleH1_ReplacedWithProperH2()
    {
        var content = string.Join("\n", new[]
        {
            "# List storage accounts",
            "<!-- @mcpcli storage account list -->",
            "",
            "Lists all storage accounts in a subscription.",
            "",
            "| Parameter | Required | Description |",
            "| --- | --- | --- |",
            "| `subscription` | Yes | The subscription ID. |"
        });

        var result = CleanupGenerator.ReplaceH2Heading(content, "List Azure Storage accounts");

        Assert.StartsWith("## List Azure Storage accounts", result);
        Assert.DoesNotContain("# List storage accounts", result);
        Assert.Contains("| Parameter | Required | Description |", result);
        Assert.Contains("Lists all storage accounts in a subscription.", result);
    }

    [Fact]
    public void ReplaceH2Heading_H1WithPhantomExamples_ExamplesStripped()
    {
        var content = string.Join("\n", new[]
        {
            "# Get key vault secret",
            "<!-- @mcpcli keyvault secret get -->",
            "",
            "Retrieves a secret from an Azure Key Vault.",
            "",
            "## Examples",
            "",
            "- Get the secret named 'api-key' from vault 'my-vault'.",
            "- Retrieve the 'connection-string' secret.",
            "",
            "| Parameter | Required | Description |",
            "| --- | --- | --- |",
            "| `vaultName` | Yes | The vault name. |",
            "| `secretName` | Yes | The secret name. |"
        });

        var result = CleanupGenerator.ReplaceH2Heading(content, "Get Key Vault secret");

        Assert.StartsWith("## Get Key Vault secret", result);
        Assert.DoesNotContain("## Examples", result);
        Assert.DoesNotContain("Get the secret named 'api-key'", result);
        // Parameter table is preserved (structural marker stops stripping)
        Assert.Contains("| Parameter | Required | Description |", result);
        Assert.Contains("| `vaultName` | Yes | The vault name. |", result);
    }

    [Fact]
    public void ReplaceH2Heading_PhantomExamplesWithParameterTableAfter_StrippingStopsAtTable()
    {
        var content = string.Join("\n", new[]
        {
            "## Query Cosmos DB container",
            "<!-- @mcpcli cosmos container query -->",
            "",
            "Queries documents in a Cosmos DB container.",
            "",
            "## Examples",
            "",
            "- Query all documents where status is 'active'.",
            "- Find documents created in the last 24 hours.",
            "",
            "| Parameter | Required | Description |",
            "| --- | --- | --- |",
            "| `accountName` | Yes | The Cosmos DB account name. |",
            "| `databaseName` | Yes | The database name. |",
            "| `containerName` | Yes | The container name. |",
            "| `query` | Yes | The SQL query string. |",
            "",
            "[Tool annotation: cosmos container query]"
        });

        var result = CleanupGenerator.ReplaceH2Heading(content, "Query Azure Cosmos DB container");

        Assert.StartsWith("## Query Azure Cosmos DB container", result);
        Assert.DoesNotContain("## Examples", result);
        Assert.DoesNotContain("Query all documents where status", result);
        // Everything after the parameter table structural marker is kept
        Assert.Contains("| `accountName` | Yes | The Cosmos DB account name. |", result);
        Assert.Contains("| `containerName` | Yes | The container name. |", result);
        Assert.Contains("[Tool annotation: cosmos container query]", result);
    }

    [Fact]
    public void ReplaceH2Heading_OnlyH1NoExtraH2_HeadingReplacedContentPreserved()
    {
        var content = string.Join("\n", new[]
        {
            "# List SQL databases",
            "<!-- @mcpcli sql database list -->",
            "",
            "Lists all SQL databases on a server.",
            "",
            "Example prompts include:",
            "",
            "- List all databases on server 'prod-sql'.",
            "",
            "| Parameter | Required | Description |",
            "| --- | --- | --- |",
            "| `serverName` | Yes | The SQL server name. |"
        });

        var result = CleanupGenerator.ReplaceH2Heading(content, "List Azure SQL databases");

        Assert.StartsWith("## List Azure SQL databases", result);
        // All original content preserved
        Assert.Contains("Lists all SQL databases on a server.", result);
        Assert.Contains("Example prompts include:", result);
        Assert.Contains("- List all databases on server 'prod-sql'.", result);
        Assert.Contains("| `serverName` | Yes | The SQL server name. |", result);
    }

    [Fact]
    public void ReplaceH2Heading_TwoPhantomH2s_BothStripped()
    {
        var content = string.Join("\n", new[]
        {
            "# Create Monitor alert",
            "<!-- @mcpcli monitor alert create -->",
            "",
            "Creates a metric alert rule.",
            "",
            "## Examples",
            "",
            "- Create an alert for CPU usage above 90%.",
            "",
            "## Overview",
            "",
            "Azure Monitor alerts notify you when conditions are met.",
            "",
            "Example prompts include:",
            "",
            "- Set up an alert rule for my web app.",
            "",
            "| Parameter | Required | Description |",
            "| --- | --- | --- |",
            "| `alertName` | Yes | The alert rule name. |"
        });

        var result = CleanupGenerator.ReplaceH2Heading(content, "Create Azure Monitor alert");

        Assert.StartsWith("## Create Azure Monitor alert", result);
        Assert.DoesNotContain("## Examples", result);
        Assert.DoesNotContain("## Overview", result);
        Assert.DoesNotContain("Create an alert for CPU usage", result);
        Assert.DoesNotContain("Azure Monitor alerts notify you", result);
        // Structural markers resume content
        Assert.Contains("Example prompts include:", result);
        Assert.Contains("| `alertName` | Yes | The alert rule name. |", result);
    }

    [Fact]
    public void ReplaceH2Heading_EmptyHeading_ReturnsContentUnchanged()
    {
        var content = string.Join("\n", new[]
        {
            "# List Redis caches",
            "<!-- @mcpcli redis cache list -->",
            "",
            "Lists all Redis caches."
        });

        var resultEmpty = CleanupGenerator.ReplaceH2Heading(content, "");
        var resultWhitespace = CleanupGenerator.ReplaceH2Heading(content, "   ");

        Assert.Equal(content, resultEmpty);
        Assert.Equal(content, resultWhitespace);
    }

    [Fact]
    public void ReplaceH2Heading_PhantomExamplesWithQuotedPrompts_AllStripped()
    {
        var content = string.Join("\n", new[]
        {
            "## List App Service plans",
            "<!-- @mcpcli appservice plan list -->",
            "",
            "Lists App Service plans in a subscription.",
            "",
            "## Examples",
            "",
            "Here are some example prompts you can use:",
            "",
            "- \"List all App Service plans in resource group 'web-rg'.\"",
            "- \"Show me the pricing tiers of my App Service plans.\"",
            "- \"How many App Service plans do I have?\"",
            "",
            "<!-- annotation-start -->",
            "[Tool annotation: appservice plan list]"
        });

        var result = CleanupGenerator.ReplaceH2Heading(content, "List Azure App Service plans");

        Assert.StartsWith("## List Azure App Service plans", result);
        Assert.DoesNotContain("## Examples", result);
        Assert.DoesNotContain("Here are some example prompts", result);
        Assert.DoesNotContain("List all App Service plans in resource group", result);
        // Structural marker (<!-- comment) stops stripping
        Assert.Contains("<!-- annotation-start -->", result);
        Assert.Contains("[Tool annotation: appservice plan list]", result);
    }

    [Fact]
    public void ReplaceH2Heading_HeadingWithHashPrefix_CleanedProperly()
    {
        var content = "# Original heading\nSome content.";

        var resultWithH2 = CleanupGenerator.ReplaceH2Heading(content, "## Already H2");
        var resultWithH1 = CleanupGenerator.ReplaceH2Heading(content, "# Was H1");
        var resultPlain = CleanupGenerator.ReplaceH2Heading(content, "Plain text heading");

        Assert.StartsWith("## Already H2", resultWithH2);
        Assert.StartsWith("## Was H1", resultWithH1);
        Assert.StartsWith("## Plain text heading", resultPlain);
        // None should have double ##
        Assert.DoesNotContain("## ## ", resultWithH2);
        Assert.DoesNotContain("## # ", resultWithH1);
    }

    [Fact]
    public void ReplaceH2Heading_StrippingStopsAtIncludeDirective()
    {
        var content = string.Join("\n", new[]
        {
            "## Get speech transcription",
            "<!-- @mcpcli speech transcription get -->",
            "",
            "Gets a speech transcription result.",
            "",
            "## Examples",
            "",
            "- Get transcription for audio file 'meeting.wav'.",
            "",
            "> [!INCLUDE [parameters](includes/speech-params.md)]",
            "",
            "| Parameter | Required | Description |",
            "| --- | --- | --- |",
            "| `transcriptionId` | Yes | The transcription ID. |"
        });

        var result = CleanupGenerator.ReplaceH2Heading(content, "Get Azure Speech transcription");

        Assert.DoesNotContain("## Examples", result);
        Assert.DoesNotContain("Get transcription for audio file", result);
        // Include directive is a structural marker — stripping stops here
        Assert.Contains("> [!INCLUDE [parameters](includes/speech-params.md)]", result);
        Assert.Contains("| `transcriptionId` | Yes | The transcription ID. |", result);
    }

    [Fact]
    public void ReplaceH2Heading_StrippingStopsAtTripleColonBlock()
    {
        var content = string.Join("\n", new[]
        {
            "## Synthesize speech",
            "<!-- @mcpcli speech synthesize -->",
            "",
            "Converts text to speech.",
            "",
            "## Usage",
            "",
            "- Synthesize 'Hello world' in English.",
            "",
            ":::zone pivot=\"programming-language-csharp\"",
            "```csharp",
            "// C# example",
            "```",
            ":::zone-end"
        });

        var result = CleanupGenerator.ReplaceH2Heading(content, "Synthesize Azure Speech");

        Assert.DoesNotContain("## Usage", result);
        Assert.DoesNotContain("Synthesize 'Hello world'", result);
        Assert.Contains(":::zone pivot=\"programming-language-csharp\"", result);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  ValidateAndFixPhantomH2Sections Tests
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateAndFix_AllToolsHaveSingleH2_NoChanges()
    {
        var family = BuildFamily("storage",
            ("account list", BuildToolContent("List storage accounts", "storage account list")),
            ("account get", BuildToolContent("Get storage account", "storage account get")),
            ("blob list", BuildToolContent("List storage blobs", "storage blob list")));

        // Capture original content
        var originalContents = family.Tools.Select(t => t.Content).ToList();

        CleanupGenerator.ValidateAndFixPhantomH2Sections(family, "  [1/1]");

        // Content should be unchanged — counts match (3 tools, 3 H2s)
        for (int i = 0; i < family.Tools.Count; i++)
        {
            Assert.Equal(originalContents[i], family.Tools[i].Content);
        }
    }

    [Fact]
    public void ValidateAndFix_OneToolHasTwoH2s_PhantomStripped()
    {
        var phantomSection = "## Examples\n\n- List all storage containers.\n- Show container details.\n";
        var contentWithPhantom = BuildToolContent(
            "List storage containers",
            "storage container list",
            extraH2Section: phantomSection);

        var family = BuildFamily("storage",
            ("container list", contentWithPhantom),
            ("container get", BuildToolContent("Get storage container", "storage container get")),
            ("account list", BuildToolContent("List storage accounts", "storage account list")));

        CleanupGenerator.ValidateAndFixPhantomH2Sections(family, "  [1/1]");

        // The phantom ## Examples should have been stripped from tool 0
        Assert.DoesNotContain("## Examples", family.Tools[0].Content);
        // The real H2 heading is kept
        Assert.Contains("## List storage containers", family.Tools[0].Content);
        // Other tools unchanged
        Assert.Contains("## Get storage container", family.Tools[1].Content);
        Assert.Contains("## List storage accounts", family.Tools[2].Content);
    }

    [Fact]
    public void ValidateAndFix_MultipleToolsWithPhantoms_AllStripped()
    {
        // Create 12 tools, 2 of which have phantom ## Examples sections
        var tools = new List<(string toolName, string content)>();

        for (int i = 1; i <= 12; i++)
        {
            var toolName = $"operation-{i:D2}";
            var heading = $"Operation {i:D2}";
            var command = $"compute vm operation-{i:D2}";

            if (i == 3 || i == 9)
            {
                // These tools have phantom ## Examples sections
                var phantom = $"## Examples\n\n- Run operation {i:D2} on VM 'prod-vm'.\n";
                tools.Add((toolName, BuildToolContent(heading, command, extraH2Section: phantom)));
            }
            else
            {
                tools.Add((toolName, BuildToolContent(heading, command)));
            }
        }

        var family = BuildFamily("compute", tools.ToArray());

        // Before: 12 tools should have 12 H2s, but we have 14 (2 phantoms)
        Assert.Equal(12, family.ToolCount);

        CleanupGenerator.ValidateAndFixPhantomH2Sections(family, "  [1/1]");

        // After: phantom H2s stripped from tools 3 and 9
        Assert.DoesNotContain("## Examples", family.Tools[2].Content);  // tool index 2 = operation-03
        Assert.DoesNotContain("## Examples", family.Tools[8].Content);  // tool index 8 = operation-09

        // All tools still have their proper H2 headings
        for (int i = 0; i < 12; i++)
        {
            Assert.Contains($"## Operation {(i + 1):D2}", family.Tools[i].Content);
        }
    }

    [Fact]
    public void ValidateAndFix_RelatedContentH2_ExcludedFromCount()
    {
        // A tool whose content includes "## Related content" — this is NOT a phantom
        var contentWithRelated = string.Join("\n", new[]
        {
            "## List Event Grid topics",
            "<!-- @mcpcli eventgrid topic list -->",
            "",
            "Lists all Event Grid topics.",
            "",
            "| Parameter | Required | Description |",
            "| --- | --- | --- |",
            "| `subscription` | Yes | The subscription ID. |",
            "",
            "## Related content",
            "",
            "- [Event Grid documentation](https://learn.microsoft.com/azure/event-grid)"
        });

        var family = BuildFamily("eventgrid",
            ("topic list", contentWithRelated),
            ("topic get", BuildToolContent("Get Event Grid topic", "eventgrid topic get")),
            ("domain list", BuildToolContent("List Event Grid domains", "eventgrid domain list")));

        // Capture original content
        var original = family.Tools[0].Content;

        CleanupGenerator.ValidateAndFixPhantomH2Sections(family, "  [1/1]");

        // "## Related content" should not be counted or stripped
        Assert.Equal(original, family.Tools[0].Content);
        Assert.Contains("## Related content", family.Tools[0].Content);
    }

    [Fact]
    public void ValidateAndFix_FewerH2sThanExpected_NoStripping()
    {
        // One tool is missing its H2 heading entirely
        var contentMissingH2 = string.Join("\n", new[]
        {
            "<!-- @mcpcli advisor recommendation list -->",
            "",
            "Lists Advisor recommendations.",
            "",
            "| Parameter | Required | Description |",
            "| --- | --- | --- |",
            "| `subscription` | Yes | The subscription ID. |"
        });

        var family = BuildFamily("advisor",
            ("recommendation list", contentMissingH2),
            ("recommendation get", BuildToolContent("Get Advisor recommendation", "advisor recommendation get")),
            ("score get", BuildToolContent("Get Advisor score", "advisor score get")));

        // Before: 3 tools but only 2 H2s — fewer than expected
        var originalContent0 = family.Tools[0].Content;

        CleanupGenerator.ValidateAndFixPhantomH2Sections(family, "  [1/1]");

        // Should not modify anything — can't auto-fix missing headings
        Assert.Equal(originalContent0, family.Tools[0].Content);
    }

    [Fact]
    public void ValidateAndFix_EmptyToolsList_NoException()
    {
        var family = new FamilyContent
        {
            FamilyName = "empty",
            Metadata = "---\ntitle: empty\n---\n# Empty family",
            RelatedContent = "## Related content",
            Tools = new List<ToolContent>()
        };

        var ex = Record.Exception(() =>
            CleanupGenerator.ValidateAndFixPhantomH2Sections(family, "  [1/1]"));

        Assert.Null(ex);
    }

    [Fact]
    public void ValidateAndFix_PhantomStrippedContentPreservesToolContent()
    {
        // Verify that after stripping a phantom, the tool's real content remains intact
        var contentWithPhantom = string.Join("\n", new[]
        {
            "## Get Key Vault key",
            "<!-- @mcpcli keyvault key get -->",
            "",
            "Retrieves a key from Azure Key Vault.",
            "",
            "## Examples",
            "",
            "- Get the key 'my-key' from vault 'prod-vault'.",
            "- Retrieve the RSA key from vault 'dev-vault'.",
            "",
            "## Overview",
            "",
            "Key Vault keys are cryptographic keys stored securely.",
            "",
            "Example prompts include:",
            "",
            "- Show the key 'signing-key' from vault 'corp-vault'.",
            "",
            "| Parameter | Required | Description |",
            "| --- | --- | --- |",
            "| `vaultName` | Yes | The vault name. |",
            "| `keyName` | Yes | The key name. |",
            "",
            "[Tool annotation: keyvault key get]"
        });

        var family = BuildFamily("keyvault",
            ("key get", contentWithPhantom),
            ("key list", BuildToolContent("List Key Vault keys", "keyvault key list")));

        CleanupGenerator.ValidateAndFixPhantomH2Sections(family, "  [1/1]");

        var result = family.Tools[0].Content;

        // Phantom sections stripped
        Assert.DoesNotContain("## Examples", result);
        Assert.DoesNotContain("## Overview", result);
        Assert.DoesNotContain("Get the key 'my-key'", result);
        Assert.DoesNotContain("Key Vault keys are cryptographic keys", result);

        // Real tool heading preserved
        Assert.Contains("## Get Key Vault key", result);
        Assert.Contains("Retrieves a key from Azure Key Vault.", result);
    }

    [Fact]
    public void ValidateAndFix_SingleToolWithPhantom_DetectsAndStrips()
    {
        // Edge case: single tool in family with a phantom H2
        var contentWithPhantom = string.Join("\n", new[]
        {
            "## Run Search query",
            "<!-- @mcpcli search query run -->",
            "",
            "Runs a search query against an Azure AI Search index.",
            "",
            "## Examples",
            "",
            "- Search for 'azure' in the 'products' index.",
        });

        var family = BuildFamily("search",
            ("query run", contentWithPhantom));

        // 1 tool but 2 H2s
        CleanupGenerator.ValidateAndFixPhantomH2Sections(family, "  [1/1]");

        Assert.Contains("## Run Search query", family.Tools[0].Content);
        Assert.DoesNotContain("## Examples", family.Tools[0].Content);
    }

    [Fact]
    public void ValidateAndFix_PhantomInMiddleOfManyTools_OnlyAffectedToolModified()
    {
        var normalTool1 = BuildToolContent("List PostgreSQL servers", "postgres server list");
        var normalTool2 = BuildToolContent("Get PostgreSQL server", "postgres server get");
        var normalTool3 = BuildToolContent("List PostgreSQL databases", "postgres database list");

        var phantomSection = "## Examples\n\n- Query the 'users' table for active accounts.\n";
        var toolWithPhantom = BuildToolContent(
            "Execute PostgreSQL query",
            "postgres query execute",
            extraH2Section: phantomSection);

        var normalTool5 = BuildToolContent("Get PostgreSQL configuration", "postgres config get");

        var family = BuildFamily("postgres",
            ("server list", normalTool1),
            ("server get", normalTool2),
            ("database list", normalTool3),
            ("query execute", toolWithPhantom),
            ("config get", normalTool5));

        // Save originals for comparison
        var originals = family.Tools.Select(t => t.Content).ToList();

        CleanupGenerator.ValidateAndFixPhantomH2Sections(family, "  [1/1]");

        // Only tool index 3 should have been modified
        Assert.Equal(originals[0], family.Tools[0].Content);
        Assert.Equal(originals[1], family.Tools[1].Content);
        Assert.Equal(originals[2], family.Tools[2].Content);
        Assert.NotEqual(originals[3], family.Tools[3].Content);  // Modified
        Assert.Equal(originals[4], family.Tools[4].Content);

        // Phantom stripped from the modified tool
        Assert.DoesNotContain("## Examples", family.Tools[3].Content);
        Assert.Contains("## Execute PostgreSQL query", family.Tools[3].Content);
    }
}
