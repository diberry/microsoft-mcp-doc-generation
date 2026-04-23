// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// TDD tests for Issue #416, Item 3: Missing HTML comments.
/// Every tool H2 should be followed by an @mcpcli comment:
///   <!-- @mcpcli {namespace} {resource} {action} -->
/// Comments must be preserved during regeneration.
/// </summary>
public class McpCliCommentInjectionTests
{
    // ── Every H2 has a comment ──────────────────────────────────────

    [Fact]
    public void InjectComments_SingleTool_AddsCommentAfterH2()
    {
        var content = "## Create a storage account\n\nThis tool creates a storage account.";
        var @namespace = "storage";
        var resource = "account";
        var action = "create";

        var result = McpCliCommentInjector.Inject(content, @namespace, resource, action);

        Assert.Contains("<!-- @mcpcli storage account create -->", result);
        // Comment should be right after the H2
        var h2Index = result.IndexOf("## Create a storage account");
        var commentIndex = result.IndexOf("<!-- @mcpcli storage account create -->");
        Assert.True(commentIndex > h2Index, "Comment should appear after H2");
    }

    [Fact]
    public void InjectComments_MultipleTools_EachH2HasComment()
    {
        var markdown =
            "## Create a resource\n\n" +
            "Description of create.\n\n" +
            "## List resources\n\n" +
            "Description of list.\n";

        var tools = new[]
        {
            ("storage", "account", "create"),
            ("storage", "account", "list"),
        };

        var result = McpCliCommentInjector.InjectAll(markdown, tools);

        Assert.Contains("<!-- @mcpcli storage account create -->", result);
        Assert.Contains("<!-- @mcpcli storage account list -->", result);
    }

    // ── Comments preserved during regeneration ──────────────────────

    [Fact]
    public void InjectComments_AlreadyPresent_DoesNotDuplicate()
    {
        var content =
            "## Create a storage account\n\n" +
            "<!-- @mcpcli storage account create -->\n\n" +
            "Description text.";

        var result = McpCliCommentInjector.Inject(content, "storage", "account", "create");

        // Should contain exactly one instance
        var count = CountOccurrences(result, "<!-- @mcpcli storage account create -->");
        Assert.Equal(1, count);
    }

    [Fact]
    public void InjectComments_CommentMissing_RestoresIt()
    {
        // Simulate AI regeneration that stripped the comment
        var content =
            "## Create a storage account\n\n" +
            "Improved description text without the comment.";

        var result = McpCliCommentInjector.Inject(content, "storage", "account", "create");

        Assert.Contains("<!-- @mcpcli storage account create -->", result);
    }

    // ── Comment format validation ───────────────────────────────────

    [Theory]
    [InlineData("fileshares", "fileshare", "create", "<!-- @mcpcli fileshares fileshare create -->")]
    [InlineData("compute", "disk", "list", "<!-- @mcpcli compute disk list -->")]
    [InlineData("keyvault", "secret", "get", "<!-- @mcpcli keyvault secret get -->")]
    public void FormatComment_ProducesCorrectFormat(
        string ns, string resource, string action, string expected)
    {
        var result = McpCliCommentInjector.FormatComment(ns, resource, action);

        Assert.Equal(expected, result);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void InjectComments_EmptyContent_ReturnsEmpty()
    {
        var result = McpCliCommentInjector.Inject("", "storage", "account", "create");

        Assert.Equal("", result);
    }

    [Fact]
    public void InjectComments_NoH2_ReturnsUnchanged()
    {
        var content = "# Top heading only\n\nSome paragraph text.\n";

        var result = McpCliCommentInjector.Inject(content, "storage", "account", "create");

        Assert.Equal(content, result);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
