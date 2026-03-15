// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolGeneration_Improved.Services;

namespace ToolGeneration_Improved.Tests;

public class TemplateLabelProtectionTests
{
    // ── ProtectTemplateLabels ──────────────────────────────────────────

    [Fact]
    public void Protect_ReplacesKnownLabels_WithTokens()
    {
        var content = "Example prompts include:\n- \"List items\"\n";
        var result = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out var map);

        Assert.DoesNotContain("Example prompts include:", result);
        Assert.Single(map);
        Assert.Contains("<<<TPL_LABEL_0>>>", result);
    }

    [Fact]
    public void Protect_HandlesMultipleLabels()
    {
        var content = """
            Example prompts include:
            - "Do something"

            Required parameters:
            --name  The name

            [Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
            Destructive: ❌
            """;
        var result = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out var map);

        Assert.Equal(3, map.Count);
        Assert.Contains("<<<TPL_LABEL_0>>>", result);
        Assert.Contains("<<<TPL_LABEL_1>>>", result);
        Assert.Contains("<<<TPL_LABEL_2>>>", result);
    }

    [Fact]
    public void Protect_EmptyContent_ReturnsEmpty()
    {
        var result = ImprovedToolGeneratorService.ProtectTemplateLabels("", out var map);

        Assert.Empty(result);
        Assert.Empty(map);
    }

    [Fact]
    public void Protect_NoLabels_ReturnsUnchanged()
    {
        var content = "# Some heading\n\nJust plain text.\n";
        var result = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out var map);

        Assert.Equal(content, result);
        Assert.Empty(map);
    }

    // ── RestoreTemplateLabels ─────────────────────────────────────────

    [Fact]
    public void Restore_ReplacesTokens_WithOriginalLabels()
    {
        var map = new Dictionary<string, string>
        {
            ["<<<TPL_LABEL_0>>>"] = "Example prompts include:"
        };
        var content = "<<<TPL_LABEL_0>>>\n- \"List items\"\n";

        var result = ImprovedToolGeneratorService.RestoreTemplateLabels(content, map);

        Assert.Contains("Example prompts include:", result);
        Assert.DoesNotContain("<<<TPL_LABEL_0>>>", result);
    }

    [Fact]
    public void Restore_EmptyMap_ReturnsUnchanged()
    {
        var content = "Some content";
        var result = ImprovedToolGeneratorService.RestoreTemplateLabels(content, new Dictionary<string, string>());

        Assert.Equal(content, result);
    }

    // ── Roundtrip: Protect → Restore ──────────────────────────────────

    [Fact]
    public void Roundtrip_ProtectThenRestore_RecoversOriginal()
    {
        var original = """
            # list

            <!-- @mcpcli advisor recommendation list -->

            List Azure Advisor recommendations.

            Example prompts include:
            - "List all recommendations"
            - "Show advisor recommendations"

            Required parameters:
            --subscription  The subscription ID

            [Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
            Destructive: ❌ | Idempotent: ✅
            """;

        var protectedContent = ImprovedToolGeneratorService.ProtectTemplateLabels(original, out var map);
        var restored = ImprovedToolGeneratorService.RestoreTemplateLabels(protectedContent, map);

        Assert.Equal(original, restored);
    }

    [Theory]
    [InlineData("Example prompts include:")]
    [InlineData("Example prompts:")]
    [InlineData("Required options:")]
    [InlineData("Optional options:")]
    [InlineData("Required parameters:")]
    [InlineData("Optional parameters:")]
    [InlineData("**Prerequisites**:")]
    [InlineData("**Success verification**:")]
    [InlineData("[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):")]
    public void Roundtrip_EachLabel_SurvivesProtectRestore(string label)
    {
        var content = $"Some intro\n\n{label}\n- item\n";

        var protectedContent = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out var map);
        var restored = ImprovedToolGeneratorService.RestoreTemplateLabels(protectedContent, map);

        Assert.Equal(content, restored);
    }

    // ── Token format resilience ───────────────────────────────────────

    [Fact]
    public void TokenFormat_DoesNotUseDoubleUnderscore()
    {
        var content = "Example prompts include:\n- \"List items\"\n";
        var result = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out _);

        // __text__ is markdown bold — AI will convert it to **text**
        Assert.DoesNotContain("__TPL_LABEL", result);
    }

    [Fact]
    public void Roundtrip_SimulatedAIBoldConversion_OldFormat_WouldFail()
    {
        // Demonstrates why __TPL_LABEL_0__ was wrong:
        // AI interprets __text__ as markdown bold and converts to **text**
        var map = new Dictionary<string, string>
        {
            ["__TPL_LABEL_0__"] = "Example prompts include:"
        };
        // Simulate what AI does: converts __x__ to **x**
        var aiOutput = "**TPL_LABEL_0**\n- \"List items\"\n";

        var restored = ImprovedToolGeneratorService.RestoreTemplateLabels(aiOutput, map);

        // The old tokens can't be found, so the broken text remains
        Assert.Contains("**TPL_LABEL_0**", restored);
        Assert.DoesNotContain("Example prompts include:", restored);
    }

    [Fact]
    public void Roundtrip_CurrentAngleBracketFormat_SurvivesAIOutput()
    {
        var content = "Example prompts include:\n- \"List items\"\n";
        var protectedContent = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out var map);

        // AI should leave <<<TPL_LABEL_0>>> alone (not markdown syntax)
        var restored = ImprovedToolGeneratorService.RestoreTemplateLabels(protectedContent, map);

        Assert.Contains("Example prompts include:", restored);
        Assert.DoesNotContain("<<<TPL_LABEL_0>>>", restored);
    }

    // ── ValidateRestoredContent ───────────────────────────────────────

    [Fact]
    public void Validate_CleanContent_ReturnsEmpty()
    {
        var content = "Example prompts include:\n- \"List items\"\n";
        var leaked = ImprovedToolGeneratorService.ValidateRestoredContent(content);

        Assert.Empty(leaked);
    }

    [Fact]
    public void Validate_EmptyContent_ReturnsEmpty()
    {
        Assert.Empty(ImprovedToolGeneratorService.ValidateRestoredContent(""));
    }

    [Theory]
    [InlineData("<<<TPL_LABEL_0>>>")]
    [InlineData("<<<TPL_LABEL_1>>>")]
    [InlineData("<<<TPL_LABEL_99>>>")]
    public void Validate_CurrentFormatLeak_DetectsToken(string token)
    {
        var content = $"Some content\n{token}\n- items\n";
        var leaked = ImprovedToolGeneratorService.ValidateRestoredContent(content);

        Assert.Single(leaked);
        Assert.Equal(token, leaked[0]);
    }

    [Theory]
    [InlineData("__TPL_LABEL_0__")]
    [InlineData("__TPL_LABEL_1__")]
    public void Validate_OldUnderscoreFormatLeak_DetectsToken(string token)
    {
        var content = $"Some content\n{token}\n- items\n";
        var leaked = ImprovedToolGeneratorService.ValidateRestoredContent(content);

        Assert.Single(leaked);
        Assert.Equal(token, leaked[0]);
    }

    [Theory]
    [InlineData("**TPL_LABEL_0**")]
    [InlineData("**TPL_LABEL_1**")]
    public void Validate_MarkdownBoldConvertedLeak_DetectsToken(string token)
    {
        var content = $"Some content\n{token}\n- items\n";
        var leaked = ImprovedToolGeneratorService.ValidateRestoredContent(content);

        Assert.Single(leaked);
        Assert.Equal(token, leaked[0]);
    }

    [Fact]
    public void Validate_MultipleLeakedTokens_DetectsAll()
    {
        var content = "<<<TPL_LABEL_0>>>\n- items\n**TPL_LABEL_1**\nannotations\n";
        var leaked = ImprovedToolGeneratorService.ValidateRestoredContent(content);

        Assert.Equal(2, leaked.Count);
        Assert.Contains("<<<TPL_LABEL_0>>>", leaked);
        Assert.Contains("**TPL_LABEL_1**", leaked);
    }

    [Fact]
    public void Validate_NormalMarkdownBold_NoClobber()
    {
        // Normal markdown bold text should NOT be flagged
        var content = "**Prerequisites**: Install Azure CLI\n**Note**: This is important\n";
        var leaked = ImprovedToolGeneratorService.ValidateRestoredContent(content);

        Assert.Empty(leaked);
    }

    // ── NormalizeTemplateLabels ────────────────────────────────────────

    [Theory]
    [InlineData("**Example prompts include:**", "Example prompts include:")]
    [InlineData("### Example prompts include:", "Example prompts include:")]
    public void Normalize_FixesMisformattedLabels(string mangled, string expected)
    {
        var content = $"Some intro\n\n{mangled}\n- items\n";
        var result = ImprovedToolGeneratorService.NormalizeTemplateLabels(content);

        Assert.Contains(expected, result);
    }

    [Fact]
    public void Normalize_EmptyContent_ReturnsEmpty()
    {
        Assert.Empty(ImprovedToolGeneratorService.NormalizeTemplateLabels(""));
    }

    // ── PR #108: Annotation Spacing Bug Regression Tests ──────────────

    [Fact]
    public void ProtectTemplateLabels_PreservesBlankLineAfterAnnotationLabel()
    {
        // Bug: The old regex pattern "\s*$" matched trailing whitespace but didn't capture
        // the newline itself, causing the blank line after the label to collapse when
        // AI processed the protected tokens.
        // Fix: Changed to "\s*\r?\n" to explicitly capture the newline character.

        var content = """
            [Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

            Destructive: ❌ | Idempotent: ✅ | Open world: ❌
            """;

        var protectedContent = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out var map);
        var restored = ImprovedToolGeneratorService.RestoreTemplateLabels(protectedContent, map);

        // The content should roundtrip perfectly - blank line preserved
        Assert.Equal(content, restored);
        
        // The map should contain the label WITH its trailing newline
        var mapValue = map.Values.First();
        Assert.EndsWith("\n", mapValue);
    }

    [Fact]
    public void ProtectTemplateLabels_PreservesBlankLineAfterExamplePromptsLabel()
    {
        var content = """
            Example prompts include:

            - "List all recommendations"
            - "Show advisor recommendations"
            """;

        var protectedContent = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out var map);
        var restored = ImprovedToolGeneratorService.RestoreTemplateLabels(protectedContent, map);

        // The content should roundtrip perfectly - blank line preserved
        Assert.Equal(content, restored);
        
        // The map should contain the label WITH its trailing newline
        var mapValue = map.Values.First();
        Assert.EndsWith("\n", mapValue);
    }

    [Fact]
    public void ProtectTemplateLabels_PreservesBlankLineAfterRequiredParametersLabel()
    {
        var content = """
            Required parameters:

            --subscription  The subscription ID
            --resource-group  The resource group name
            """;

        var protectedContent = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out var map);
        var restored = ImprovedToolGeneratorService.RestoreTemplateLabels(protectedContent, map);

        // The content should roundtrip perfectly - blank line preserved
        Assert.Equal(content, restored);
        
        // The map should contain the label WITH its trailing newline
        var mapValue = map.Values.First();
        Assert.EndsWith("\n", mapValue);
    }

    [Fact]
    public void ProtectTemplateLabels_PreservesBlankLineAfterOptionalParametersLabel()
    {
        var content = """
            Optional parameters:

            --filter  Filter expression
            --output  Output format
            """;

        var protectedContent = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out var map);
        var restored = ImprovedToolGeneratorService.RestoreTemplateLabels(protectedContent, map);

        // The content should roundtrip perfectly - blank line preserved
        Assert.Equal(content, restored);
        
        // The map should contain the label WITH its trailing newline
        var mapValue = map.Values.First();
        Assert.EndsWith("\n", mapValue);
    }

    [Fact]
    public void ProtectTemplateLabels_PreservesBlankLineAfterPrerequisitesLabel()
    {
        var content = """
            **Prerequisites**:

            - Azure CLI installed
            - Valid Azure subscription
            """;

        var protectedContent = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out var map);
        var restored = ImprovedToolGeneratorService.RestoreTemplateLabels(protectedContent, map);

        // The content should roundtrip perfectly - blank line preserved
        Assert.Equal(content, restored);
        
        // The map should contain the label WITH its trailing newline
        var mapValue = map.Values.First();
        Assert.EndsWith("\n", mapValue);
    }

    [Fact]
    public void ProtectTemplateLabels_PreservesAllBlankLinesInMultiLabelDocument()
    {
        // Comprehensive test covering the full tool file structure
        var content = """
            # advisor recommendation list

            List Azure Advisor recommendations.

            Example prompts include:

            - "List all recommendations"
            - "Show advisor recommendations"

            Required parameters:

            --subscription  The subscription ID

            Optional parameters:

            --filter  Filter expression

            **Prerequisites**:

            - Azure CLI installed

            [Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

            Destructive: ❌ | Idempotent: ✅ | Open world: ❌
            """;

        var protectedContent = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out var map);
        var restored = ImprovedToolGeneratorService.RestoreTemplateLabels(protectedContent, map);

        // The content should roundtrip perfectly - all blank lines preserved
        Assert.Equal(content, restored);
        
        // All map values should end with newlines
        Assert.All(map.Values, value => Assert.EndsWith("\n", value));
    }

    [Fact]
    public void ProtectTemplateLabels_WindowsLineEndings_PreservesBlankLine()
    {
        // Test with Windows-style \r\n line endings
        var content = "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):\r\n\r\nDestructive: ❌";

        var protectedContent = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out var map);
        var restored = ImprovedToolGeneratorService.RestoreTemplateLabels(protectedContent, map);

        // The content should roundtrip perfectly with Windows line endings
        Assert.Equal(content, restored);
    }

    [Fact]
    public void ProtectTemplateLabels_UnixLineEndings_PreservesBlankLine()
    {
        // Test with Unix-style \n line endings
        var content = "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):\n\nDestructive: ❌";

        var protectedContent = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out var map);
        var restored = ImprovedToolGeneratorService.RestoreTemplateLabels(protectedContent, map);

        // The content should roundtrip perfectly with Unix line endings
        Assert.Equal(content, restored);
    }

    [Fact]
    public void ProtectTemplateLabels_LabelWithoutBlankLine_StillWorks()
    {
        // Edge case: label immediately followed by content (no blank line)
        var content = "Example prompts include:\n- \"List items\"";

        var protectedContent = ImprovedToolGeneratorService.ProtectTemplateLabels(content, out var map);
        var restored = ImprovedToolGeneratorService.RestoreTemplateLabels(protectedContent, map);

        // Should still work correctly even without a blank line
        Assert.Equal(content, restored);
    }

    [Fact]
    public void ExtractRequiredParameters_ReturnsRequiredRowsFromParameterTable()
    {
        var content = """
            # foundry agents connect

            | Parameter | Required or optional | Description |
            |-----------|---------------------|-------------|
            | **Agent ID** | Required | The ID of the agent to interact with. |
            | **Query** | Required | The query sent to the agent. |
            | **Max results** | Optional | Maximum number of results to return. |
            """;

        var requiredParameters = ImprovedToolGeneratorService.ExtractRequiredParameters(content);

        Assert.Equal(2, requiredParameters.Count);
        Assert.Equal("Agent ID", requiredParameters[0].DisplayName);
        Assert.Equal("Query", requiredParameters[1].DisplayName);
    }

    [Fact]
    public void FindMissingRequiredParameters_DetectsDroppedRequiredRows()
    {
        var originalContent = """
            | Parameter | Required or optional | Description |
            |-----------|---------------------|-------------|
            | **Agent ID** | Required | The ID of the agent to interact with. |
            | **Query** | Required | The query sent to the agent. |
            | **Max results** | Optional | Maximum number of results to return. |
            """;
        var improvedContent = """
            | Parameter | Required or optional | Description |
            |-----------|---------------------|-------------|
            | **Agent ID** | Required | The ID of the agent to interact with. |
            | **Max results** | Optional | Maximum number of results to return. |
            """;

        var requiredParameters = ImprovedToolGeneratorService.ExtractRequiredParameters(originalContent);
        var missingParameters = ImprovedToolGeneratorService.FindMissingRequiredParameters(improvedContent, requiredParameters);

        Assert.Single(missingParameters);
        Assert.Equal("Query", missingParameters[0].DisplayName);
    }

    [Fact]
    public void ReinjectMissingRequiredParameters_RestoresDroppedRowsInExistingTable()
    {
        var originalContent = """
            # foundry agents connect

            Example prompts include:

            - "Connect to the analytics agent"

            | Parameter | Required or optional | Description |
            |-----------|---------------------|-------------|
            | **Agent ID** | Required | The ID of the agent to interact with. |
            | **Query** | Required | The query sent to the agent. |
            | **Max results** | Optional | Maximum number of results to return. |

            [Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
            Destructive: ❌
            """;
        var improvedContent = """
            # foundry agents connect

            Example prompts include:

            - "Connect to the analytics agent"

            | Parameter | Required or optional | Description |
            |-----------|---------------------|-------------|
            | **Agent ID** | Required | The ID of the agent to interact with. |
            | **Max results** | Optional | Maximum number of results to return. |

            [Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
            Destructive: ❌
            """;

        var requiredParameters = ImprovedToolGeneratorService.ExtractRequiredParameters(originalContent);
        var missingParameters = ImprovedToolGeneratorService.FindMissingRequiredParameters(improvedContent, requiredParameters);
        var repairedContent = ImprovedToolGeneratorService.ReinjectMissingRequiredParameters(improvedContent, originalContent, missingParameters);

        Assert.Contains("| **Query** | Required | The query sent to the agent. |", repairedContent);
        Assert.Empty(ImprovedToolGeneratorService.FindMissingRequiredParameters(repairedContent, requiredParameters));
    }

    [Fact]
    public void ReinjectMissingRequiredParameters_RestoresTableWhenAiDropsItEntirely()
    {
        var originalContent = """
            # foundry agents connect

            Example prompts include:

            - "Connect to the analytics agent"

            | Parameter | Required or optional | Description |
            |-----------|---------------------|-------------|
            | **Agent ID** | Required | The ID of the agent to interact with. |
            | **Query** | Required | The query sent to the agent. |

            [Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
            Destructive: ❌
            """;
        var improvedContent = """
            # foundry agents connect

            Example prompts include:

            - "Connect to the analytics agent"

            [Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):
            Destructive: ❌
            """;

        var requiredParameters = ImprovedToolGeneratorService.ExtractRequiredParameters(originalContent);
        var repairedContent = ImprovedToolGeneratorService.ReinjectMissingRequiredParameters(improvedContent, originalContent, requiredParameters);

        Assert.Contains("| Parameter | Required or optional | Description |", repairedContent);
        Assert.Contains("| **Query** | Required | The query sent to the agent. |", repairedContent);
        Assert.Empty(ImprovedToolGeneratorService.FindMissingRequiredParameters(repairedContent, requiredParameters));
    }
}
