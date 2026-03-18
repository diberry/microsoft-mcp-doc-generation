// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolGeneration_Improved.Services;

namespace ToolGeneration_Improved.Tests;

public class ExamplePromptSectionFreezeTests
{
    // ── ProtectExamplePromptSections ──────────────────────────────────

    [Fact]
    public void Protect_FreezesSectionWithIncludeVariant()
    {
        var content =
            "Some intro text\n" +
            "Example prompts include:\n" +
            "- List all items in the resource group\n" +
            "- Show details for item 'my-item'\n" +
            "\n" +
            "Required parameters:\n";

        var result = ImprovedToolGeneratorService.ProtectExamplePromptSections(content, out var map);

        Assert.Single(map);
        Assert.Contains("<<<FROZEN_SECTION_0>>>", result);
        Assert.DoesNotContain("Example prompts include:", result);
        Assert.DoesNotContain("- List all items", result);
    }

    [Fact]
    public void Protect_FreezesSectionWithColonOnlyVariant()
    {
        var content =
            "Some intro text\n" +
            "Example prompts:\n" +
            "- Create a new resource named 'test-resource'\n" +
            "- Delete the resource with ID 'abc-123'\n" +
            "\n" +
            "Optional parameters:\n";

        var result = ImprovedToolGeneratorService.ProtectExamplePromptSections(content, out var map);

        Assert.Single(map);
        Assert.Contains("<<<FROZEN_SECTION_0>>>", result);
        Assert.DoesNotContain("Example prompts:", result);
        Assert.DoesNotContain("- Create a new resource", result);
    }

    [Fact]
    public void Protect_NoExamplePrompts_ReturnsUnchanged()
    {
        var content =
            "# Tool documentation\n" +
            "\n" +
            "Some description of the tool.\n" +
            "\n" +
            "Required parameters:\n" +
            "- name: The resource name\n";

        var result = ImprovedToolGeneratorService.ProtectExamplePromptSections(content, out var map);

        Assert.Equal(content, result);
        Assert.Empty(map);
    }

    [Fact]
    public void Protect_MultipleSections_EachGetOwnToken()
    {
        var content =
            "## Tool A\n" +
            "Example prompts include:\n" +
            "- Query items from database 'prod-db'\n" +
            "- List records in table 'users'\n" +
            "\n" +
            "## Tool B\n" +
            "Example prompts:\n" +
            "- Update configuration for endpoint 'api-gateway'\n" +
            "- Reset credentials for service 'auth-svc'\n" +
            "\n";

        var result = ImprovedToolGeneratorService.ProtectExamplePromptSections(content, out var map);

        Assert.Equal(2, map.Count);
        Assert.Contains("<<<FROZEN_SECTION_0>>>", result);
        Assert.Contains("<<<FROZEN_SECTION_1>>>", result);
    }

    // ── Roundtrip ────────────────────────────────────────────────────

    [Fact]
    public void FreezeAndRestore_RoundtripPreservesContent()
    {
        var original =
            "# Documentation\n" +
            "\n" +
            "Example prompts include:\n" +
            "- Deploy application to region 'eastus'\n" +
            "- Scale instance count to '3' for service 'web-app'\n" +
            "\n" +
            "Required parameters:\n" +
            "- region: Target region\n";

        var frozen = ImprovedToolGeneratorService.ProtectExamplePromptSections(original, out var map);
        Assert.NotEqual(original, frozen);

        var restored = ImprovedToolGeneratorService.RestoreExamplePromptSections(frozen, map);
        Assert.Equal(original, restored);
    }

    // ── Leak detection ───────────────────────────────────────────────

    [Fact]
    public void ValidateRestoredContent_DetectsLeakedFrozenSectionTokens()
    {
        var content = "Some text <<<FROZEN_SECTION_0>>> more text";

        var leaked = ImprovedToolGeneratorService.ValidateRestoredContent(content);

        Assert.Single(leaked);
        Assert.Equal("<<<FROZEN_SECTION_0>>>", leaked[0]);
    }

    [Fact]
    public void ValidateRestoredContent_DetectsBothTokenTypes()
    {
        var content = "<<<TPL_LABEL_0>>> and <<<FROZEN_SECTION_1>>>";

        var leaked = ImprovedToolGeneratorService.ValidateRestoredContent(content);

        Assert.Equal(2, leaked.Count);
        Assert.Contains("<<<TPL_LABEL_0>>>", leaked);
        Assert.Contains("<<<FROZEN_SECTION_1>>>", leaked);
    }

    // ── Edge cases ───────────────────────────────────────────────────

    [Fact]
    public void Protect_EmptyContent_ReturnsEmpty()
    {
        var result = ImprovedToolGeneratorService.ProtectExamplePromptSections("", out var map);

        Assert.Empty(result);
        Assert.Empty(map);
    }

    [Fact]
    public void Restore_EmptyMap_ReturnsUnchanged()
    {
        var content = "Some content with <<<FROZEN_SECTION_0>>>";
        var emptyMap = new Dictionary<string, string>();

        var result = ImprovedToolGeneratorService.RestoreExamplePromptSections(content, emptyMap);

        Assert.Equal(content, result);
    }
}
