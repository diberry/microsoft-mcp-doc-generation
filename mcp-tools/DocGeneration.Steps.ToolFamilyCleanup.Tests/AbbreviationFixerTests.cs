// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for AbbreviationFixer — deterministic post-processor that
/// replaces Latin abbreviations with spelled-out forms per Microsoft
/// style guide. Fixes Acrolinx "Avoid Latin abbreviations" rule.
/// </summary>
public class AbbreviationFixerTests
{
    // ── Core replacements: e.g. ────────────────────────────────────

    [Theory]
    [InlineData("Use Azure Storage (e.g., Blob, Queue)", "Use Azure Storage (for example, Blob, Queue)")]
    [InlineData("Several regions e.g., eastus, westus2", "Several regions for example, eastus, westus2")]
    [InlineData("Tools e.g. list, create, delete", "Tools for example, list, create, delete")]
    [InlineData("Use the command e.g. az storage account", "Use the command for example, az storage account")]
    public void Fix_EgAbbreviation_ReplacedWithForExample(string input, string expected)
    {
        var result = AbbreviationFixer.Fix(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Fix_EgInParentheses_RealWorldExample()
    {
        var input = "Specify a region (e.g., 'eastus', 'westus2') for the resource.";
        var result = AbbreviationFixer.Fix(input);
        Assert.Equal("Specify a region (for example, 'eastus', 'westus2') for the resource.", result);
    }

    // ── Core replacements: i.e. ────────────────────────────────────

    [Theory]
    [InlineData("The default value i.e., 30 seconds", "The default value that is, 30 seconds")]
    [InlineData("Use the primary region i.e. the closest one", "Use the primary region that is, the closest one")]
    [InlineData("A single resource i.e., a storage account", "A single resource that is, a storage account")]
    public void Fix_IeAbbreviation_ReplacedWithThatIs(string input, string expected)
    {
        var result = AbbreviationFixer.Fix(input);
        Assert.Equal(expected, result);
    }

    // ── Core replacements: etc. ────────────────────────────────────

    [Theory]
    [InlineData("Include regions like eastus, westus, etc.", "Include regions like eastus, westus, ")]
    [InlineData("Tools include list, create, delete, etc. for management", "Tools include list, create, delete, and more for management")]
    [InlineData("Parameters: name, location, etc.", "Parameters: name, location, ")]
    public void Fix_EtcAbbreviation_RemovedOrReplacedWithAndMore(string input, string expected)
    {
        var result = AbbreviationFixer.Fix(input);
        Assert.Equal(expected, result);
    }

    // ── Idempotency ────────────────────────────────────────────────

    [Theory]
    [InlineData("Use Azure Storage (for example, Blob, Queue)")]
    [InlineData("The default value that is, 30 seconds")]
    [InlineData("Tools include list, create, delete")]
    public void Fix_AlreadySpelledOut_NoChange(string input)
    {
        var result = AbbreviationFixer.Fix(input);
        Assert.Equal(input, result);
    }

    // ── Backtick protection ────────────────────────────────────────

    [Fact]
    public void Fix_InsideBackticks_NotReplaced()
    {
        var input = "Use the parameter `e.g.` in your command.";
        var result = AbbreviationFixer.Fix(input);
        Assert.Contains("`e.g.`", result);
    }

    [Fact]
    public void Fix_MultipleBacktickSpans_ProtectedCorrectly()
    {
        var input = "Examples: `e.g.` and `i.e.` are abbreviations, e.g., use them carefully.";
        var result = AbbreviationFixer.Fix(input);
        Assert.Contains("`e.g.`", result);
        Assert.Contains("`i.e.`", result);
        Assert.Contains("for example, use", result);
    }

    // ── Code block protection ──────────────────────────────────────

    [Fact]
    public void Fix_InsideCodeBlock_NotReplaced()
    {
        var input = @"Text with e.g. here.

```bash
# Comment with e.g., i.e., etc. should not be changed
echo ""e.g., test""
```

More text with e.g. outside.";

        var result = AbbreviationFixer.Fix(input);

        // Inside code block should NOT be replaced
        Assert.Contains("# Comment with e.g., i.e., etc. should not be changed", result);
        Assert.Contains(@"echo ""e.g., test""", result);

        // Outside code block SHOULD be replaced
        Assert.Contains("Text with for example, here.", result);
        Assert.Contains("More text with for example, outside.", result);
    }

    // ── Edge cases ─────────────────────────────────────────────────

    [Fact]
    public void Fix_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", AbbreviationFixer.Fix(""));
        Assert.Equal("", AbbreviationFixer.Fix(null!));
    }

    [Fact]
    public void Fix_NoMatchingPatterns_ReturnsUnchanged()
    {
        var input = "This is a normal sentence about Azure resources.";
        var result = AbbreviationFixer.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_MultipleOccurrences_AllReplaced()
    {
        var input = "Use tools e.g., list, create, i.e., CRUD operations, etc.";
        var result = AbbreviationFixer.Fix(input);
        Assert.Contains("for example,", result);
        Assert.Contains("that is,", result);
        Assert.DoesNotContain("etc.", result);
    }

    [Fact]
    public void Fix_MixedCaseAbbreviations_ReplacedCorrectly()
    {
        var input = "Examples E.g., Azure Storage and I.e., Blob service";
        var result = AbbreviationFixer.Fix(input);
        Assert.Contains("for example,", result);
        Assert.Contains("that is,", result);
    }

    [Fact]
    public void Fix_EtcInListContext_RemovesCleanly()
    {
        var input = "Supported services: Storage, Key Vault, Cosmos DB, etc.";
        var result = AbbreviationFixer.Fix(input);
        // Should remove ", etc." leaving the list clean
        Assert.DoesNotContain("etc.", result);
        Assert.Contains("Cosmos DB, ", result);
    }

    [Fact]
    public void Fix_EtcStandalone_ReplacesWithAndMore()
    {
        var input = "Many other services etc. are supported.";
        var result = AbbreviationFixer.Fix(input);
        Assert.Contains("and more", result);
        Assert.DoesNotContain("etc.", result);
    }
}
