// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolGeneration_Improved.Services;

namespace ToolGeneration_Improved.Tests;

/// <summary>
/// Tests for ProtectParameterTable / RestoreParameterTable — Fix 1 for bugs #554 and #558.
/// Verifies that parameter tables are frozen before AI processing and restored unchanged after,
/// preventing Required/Optional flips (#554) and display-name rewrites (#558).
/// </summary>
public class ParameterTableFreezeTests
{
    // ── ProtectParameterTable ─────────────────────────────────────────

    [Fact]
    public void Protect_FreezesBasicParameterTable()
    {
        var content =
            "## Parameters\n" +
            "\n" +
            "| Parameter | Required or optional | Description |\n" +
            "|---|---|---|\n" +
            "| `--vault` | Required | Key vault name |\n" +
            "| `--resource-group` | Optional | Resource group name |\n" +
            "\n" +
            "Some text after.\n";

        var result = ImprovedToolGeneratorService.ProtectParameterTable(content, out var map);

        Assert.Single(map);
        Assert.Contains("<<<FROZEN_PARAM_TABLE_0>>>", result);
        Assert.DoesNotContain("| Parameter |", result);
        Assert.DoesNotContain("| `--vault` |", result);
    }

    [Fact]
    public void Protect_NoTable_ReturnsUnchanged()
    {
        var content =
            "# Tool documentation\n" +
            "\n" +
            "Some description without any parameter table.\n";

        var result = ImprovedToolGeneratorService.ProtectParameterTable(content, out var map);

        Assert.Equal(content, result);
        Assert.Empty(map);
    }

    [Fact]
    public void Protect_IncludesFootnoteImmediatelyAfterTable()
    {
        var content =
            "| Parameter | Required or optional | Description |\n" +
            "|---|---|---|\n" +
            "| `--server` | Required* | Server name |\n" +
            "* Required when not using managed identity.\n" +
            "\n" +
            "Other content.\n";

        var result = ImprovedToolGeneratorService.ProtectParameterTable(content, out var map);

        Assert.Single(map);
        var frozen = map.Values.Single();
        Assert.Contains("* Required when not using managed identity.", frozen);
        Assert.DoesNotContain("* Required when not using managed identity.", result);
    }

    [Fact]
    public void Protect_IncludesFootnoteAfterBlankLine()
    {
        var content =
            "| Parameter | Required or optional | Description |\n" +
            "|---|---|---|\n" +
            "| `--subscription` | Required* | Subscription ID |\n" +
            "\n" +
            "* Required when subscription cannot be inferred from context.\n" +
            "\n" +
            "Other content.\n";

        var result = ImprovedToolGeneratorService.ProtectParameterTable(content, out var map);

        Assert.Single(map);
        var frozen = map.Values.Single();
        Assert.Contains("* Required when subscription cannot be inferred from context.", frozen);
    }

    [Fact]
    public void Protect_StopsAtNonTableLine_NotAtNextHeading()
    {
        var content =
            "| Parameter | Required or optional | Description |\n" +
            "|---|---|---|\n" +
            "| `--name` | Required | Resource name |\n" +
            "\n" +
            "## Next section\n" +
            "More prose here.\n";

        var result = ImprovedToolGeneratorService.ProtectParameterTable(content, out var map);

        Assert.Single(map);
        var frozen = map.Values.Single();
        // Table block ends at the blank line — heading stays outside the frozen block
        Assert.DoesNotContain("## Next section", frozen);
        Assert.Contains("## Next section", result);
    }

    [Fact]
    public void Protect_MultipleTablesGetSeparateTokens()
    {
        var content =
            "## Tool A\n" +
            "\n" +
            "| Parameter | Required or optional | Description |\n" +
            "|---|---|---|\n" +
            "| `--server` | Required | Server hostname |\n" +
            "\n" +
            "## Tool B\n" +
            "\n" +
            "| Parameter | Required or optional | Description |\n" +
            "|---|---|---|\n" +
            "| `--database` | Required | Database name |\n" +
            "| `--port` | Optional | Port number |\n" +
            "\n";

        var result = ImprovedToolGeneratorService.ProtectParameterTable(content, out var map);

        Assert.Equal(2, map.Count);
        Assert.Contains("<<<FROZEN_PARAM_TABLE_0>>>", result);
        Assert.Contains("<<<FROZEN_PARAM_TABLE_1>>>", result);
        Assert.DoesNotContain("| `--server` |", result);
        Assert.DoesNotContain("| `--database` |", result);
    }

    [Fact]
    public void Protect_EmptyContent_ReturnsEmpty()
    {
        var result = ImprovedToolGeneratorService.ProtectParameterTable("", out var map);

        Assert.Empty(result);
        Assert.Empty(map);
    }

    // ── Roundtrip ────────────────────────────────────────────────────

    [Fact]
    public void FreezeAndRestore_RoundtripPreservesTableExactly()
    {
        var original =
            "## Parameters\n" +
            "\n" +
            "| Parameter | Required or optional | Description |\n" +
            "|---|---|---|\n" +
            "| `--raw-mcp-tool-input` | Optional | Raw JSON input for the tool |\n" +
            "| `--is-azd-project` | Optional | Use azd project pipeline configuration |\n" +
            "\n" +
            "More content after the table.\n";

        var frozen = ImprovedToolGeneratorService.ProtectParameterTable(original, out var map);
        Assert.NotEqual(original, frozen);

        var restored = ImprovedToolGeneratorService.RestoreParameterTable(frozen, map);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void FreezeAndRestore_RoundtripWithFootnote_PreservesExactly()
    {
        var original =
            "| Parameter | Required or optional | Description |\n" +
            "|---|---|---|\n" +
            "| `--vault` | Required | Key vault name |\n" +
            "| `--subscription` | Required* | Azure subscription ID |\n" +
            "\n" +
            "* Required when subscription cannot be inferred from the environment.\n" +
            "\n" +
            "Other content.\n";

        var frozen = ImprovedToolGeneratorService.ProtectParameterTable(original, out var map);
        var restored = ImprovedToolGeneratorService.RestoreParameterTable(frozen, map);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void FreezeAndRestore_MultipleTablesRoundtripPreservesAll()
    {
        var original =
            "## AKS\n" +
            "\n" +
            "| Parameter | Required or optional | Description |\n" +
            "|---|---|---|\n" +
            "| `--cluster-name` | Required | AKS cluster name |\n" +
            "\n" +
            "## Storage\n" +
            "\n" +
            "| Parameter | Required or optional | Description |\n" +
            "|---|---|---|\n" +
            "| `--account-name` | Required | Storage account name |\n" +
            "| `--container` | Optional | Container name |\n" +
            "\n";

        var frozen = ImprovedToolGeneratorService.ProtectParameterTable(original, out var map);
        var restored = ImprovedToolGeneratorService.RestoreParameterTable(frozen, map);

        Assert.Equal(original, restored);
    }

    // ── AI cannot modify frozen content ──────────────────────────────

    [Fact]
    public void FrozenContent_SimulatedAiRewrite_RequiredFlagPreserved()
    {
        // Simulate what the AI might do: flip Required → Optional in the token text.
        // Since the token replaces the table, the AI sees only the token — it cannot edit the table.
        var original =
            "| Parameter | Required or optional | Description |\n" +
            "|---|---|---|\n" +
            "| `--loadtest-resource` | Required | Load test resource name |\n" +
            "\n";

        var frozen = ImprovedToolGeneratorService.ProtectParameterTable(original, out var map);

        // Simulate AI returning the frozen token unchanged (correct) or trying to "help" with prose around it
        var simulatedAiOutput = "Here is the improved content.\n\n" + frozen + "\nEnd of content.\n";

        var restored = ImprovedToolGeneratorService.RestoreParameterTable(simulatedAiOutput, map);

        // The original table with "Required" must appear in the restored content unchanged
        Assert.Contains("| `--loadtest-resource` | Required |", restored);
    }

    // ── Leak detection ───────────────────────────────────────────────

    [Fact]
    public void ValidateRestoredContent_DetectsLeakedParamTableToken()
    {
        // If restore fails (token not replaced), leaked-token check must catch it
        var contentWithLeakedToken = "Some text\n<<<FROZEN_PARAM_TABLE_0>>>\nMore text";

        var leaked = ImprovedToolGeneratorService.ValidateRestoredContent(contentWithLeakedToken);

        Assert.Single(leaked);
        Assert.Equal("<<<FROZEN_PARAM_TABLE_0>>>", leaked[0]);
    }

    [Fact]
    public void ValidateRestoredContent_DetectsAllLeakedTokenTypes()
    {
        var content = "<<<TPL_LABEL_0>>> and <<<FROZEN_SECTION_1>>> and <<<FROZEN_PARAM_TABLE_2>>>";

        var leaked = ImprovedToolGeneratorService.ValidateRestoredContent(content);

        Assert.Equal(3, leaked.Count);
        Assert.Contains("<<<TPL_LABEL_0>>>", leaked);
        Assert.Contains("<<<FROZEN_SECTION_1>>>", leaked);
        Assert.Contains("<<<FROZEN_PARAM_TABLE_2>>>", leaked);
    }

    // ── Restore edge cases ───────────────────────────────────────────

    [Fact]
    public void Restore_EmptyMap_ReturnsUnchanged()
    {
        var content = "Content with <<<FROZEN_PARAM_TABLE_0>>> still present";
        var emptyMap = new Dictionary<string, string>();

        var result = ImprovedToolGeneratorService.RestoreParameterTable(content, emptyMap);

        Assert.Equal(content, result);
    }
}
