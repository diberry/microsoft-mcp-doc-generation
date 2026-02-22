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
}
