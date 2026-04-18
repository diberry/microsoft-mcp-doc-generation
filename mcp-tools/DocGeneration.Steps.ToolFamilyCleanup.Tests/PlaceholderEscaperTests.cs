// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for PlaceholderEscaper — escapes bare angle-bracket placeholders
/// that MS Learn build validation flags as disallowed HTML tags.
/// Fixes: #416
/// </summary>
public class PlaceholderEscaperTests
{
    // ── Basic escaping ──────────────────────────────────────────────

    [Theory]
    [InlineData(
        "Use <resource-name> for the name.",
        @"Use \<resource-name\> for the name.")]
    [InlineData(
        "Set <subscription-id> to your ID.",
        @"Set \<subscription-id\> to your ID.")]
    [InlineData(
        "Provide <your-resource-group>.",
        @"Provide \<your-resource-group\>.")]
    [InlineData(
        "Enter <cluster-name> and <region>.",
        @"Enter \<cluster-name\> and \<region\>.")]
    public void Escape_BarePlaceholder_BackslashEscaped(string input, string expected)
    {
        var result = PlaceholderEscaper.Escape(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Escape_PlaceholderWithUnderscores_Escaped()
    {
        var input = "Use <my_resource_name> here.";
        var result = PlaceholderEscaper.Escape(input);
        Assert.Equal(@"Use \<my_resource_name\> here.", result);
    }

    [Fact]
    public void Escape_PlaceholderWithDigits_Escaped()
    {
        var input = "Use <server1> for testing.";
        var result = PlaceholderEscaper.Escape(input);
        Assert.Equal(@"Use \<server1\> for testing.", result);
    }

    // ── Already escaped — idempotent ────────────────────────────────

    [Fact]
    public void Escape_AlreadyBackslashEscaped_NotDoubleEscaped()
    {
        var input = @"Use \<resource-name\> for the name.";
        var result = PlaceholderEscaper.Escape(input);
        Assert.Equal(input, result);
    }

    // ── Protected content: code fences ──────────────────────────────

    [Fact]
    public void Escape_InsideCodeFence_NotEscaped()
    {
        var input = "```\naz group create --name <resource-group>\n```";
        var result = PlaceholderEscaper.Escape(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Escape_MixedCodeFenceAndProse_OnlyProseEscaped()
    {
        var input = "Set <resource-name> first.\n\n```bash\naz vm create --name <vm-name>\n```\n\nThen use <region>.";
        var result = PlaceholderEscaper.Escape(input);
        Assert.Contains(@"\<resource-name\>", result);
        Assert.Contains("<vm-name>", result); // inside code fence — preserved
        Assert.Contains(@"\<region\>", result);
    }

    // ── Protected content: inline code ──────────────────────────────

    [Fact]
    public void Escape_InsideInlineCode_NotEscaped()
    {
        var input = "Run `az group create --name <resource-group>` to create.";
        var result = PlaceholderEscaper.Escape(input);
        Assert.Contains("`az group create --name <resource-group>`", result);
    }

    [Fact]
    public void Escape_MixedInlineCodeAndProse_OnlyProseEscaped()
    {
        var input = "Use `<resource-name>` or provide <resource-name> directly.";
        var result = PlaceholderEscaper.Escape(input);
        Assert.Contains("`<resource-name>`", result); // inside backticks — preserved
        Assert.Contains(@"\<resource-name\>", result); // bare in prose — escaped
    }

    // ── Protected content: HTML comments ────────────────────────────

    [Fact]
    public void Escape_HtmlComment_NotEscaped()
    {
        var input = "<!-- @mcpcli azmcp monitor workspace list -->\nUse <resource-name>.";
        var result = PlaceholderEscaper.Escape(input);
        Assert.Contains("<!-- @mcpcli azmcp monitor workspace list -->", result);
        Assert.Contains(@"\<resource-name\>", result);
    }

    [Fact]
    public void Escape_NonMcpcliHtmlComment_NotEscaped()
    {
        var input = "<!-- This comment has <placeholder> in it -->\nUse <name>.";
        var result = PlaceholderEscaper.Escape(input);
        Assert.Contains("<!-- This comment has <placeholder> in it -->", result);
        Assert.Contains(@"\<name\>", result);
    }

    // ── Protected content: frontmatter ──────────────────────────────

    [Fact]
    public void Escape_Frontmatter_NotEscaped()
    {
        var input = "---\ntitle: Use <resource-name>\n---\n\nProvide <resource-name>.";
        var result = PlaceholderEscaper.Escape(input);
        Assert.Contains("title: Use <resource-name>", result);
        Assert.Contains(@"\<resource-name\>", result);
    }

    // ── Non-placeholder angle brackets — NOT escaped ────────────────

    [Fact]
    public void Escape_UppercaseHtmlTag_NotEscaped()
    {
        // Pattern only matches lowercase starting char
        var input = "The <A href='url'>link</A> goes here.";
        var result = PlaceholderEscaper.Escape(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Escape_ComparisonOperators_NotEscaped()
    {
        // Pattern requires [a-z] as first char; "5" doesn't match
        var input = "Use values where x < 10 and y > 5.";
        var result = PlaceholderEscaper.Escape(input);
        Assert.Equal(input, result);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Escape_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal("", PlaceholderEscaper.Escape(""));
        Assert.Equal("", PlaceholderEscaper.Escape(null!));
    }

    [Fact]
    public void Escape_NoPlaceholders_ReturnsUnchanged()
    {
        var input = "This is a normal sentence about Azure resources.";
        var result = PlaceholderEscaper.Escape(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Escape_MultiplePlaceholders_AllEscaped()
    {
        var input = "Set <resource-group>, <location>, and <name>.";
        var result = PlaceholderEscaper.Escape(input);
        Assert.Equal(@"Set \<resource-group\>, \<location\>, and \<name\>.", result);
    }

    [Fact]
    public void Escape_Idempotent_RunTwice()
    {
        var input = "Use <resource-name> here.";
        var first = PlaceholderEscaper.Escape(input);
        var second = PlaceholderEscaper.Escape(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void Escape_ComplexDocument_MixedContent()
    {
        var input = """
            ---
            title: Create <resource-name>
            ---

            ## Create a resource

            Provide <resource-name> and <location>.

            ```bash
            az group create --name <resource-group> --location <location>
            ```

            Use `<subscription-id>` from your account, or enter <subscription-id> directly.

            <!-- @mcpcli azmcp monitor workspace list -->
            """;

        var result = PlaceholderEscaper.Escape(input);

        // Frontmatter preserved
        Assert.Contains("title: Create <resource-name>", result);
        // Prose escaped
        Assert.Contains(@"\<resource-name\>", result);
        Assert.Contains(@"\<location\>", result);
        // Code fence preserved
        Assert.Contains("--name <resource-group>", result);
        // Inline code preserved
        Assert.Contains("`<subscription-id>`", result);
        // Bare prose version escaped
        Assert.Contains(@"\<subscription-id\>", result);
        // HTML comment preserved
        Assert.Contains("<!-- @mcpcli azmcp monitor workspace list -->", result);
    }
}
