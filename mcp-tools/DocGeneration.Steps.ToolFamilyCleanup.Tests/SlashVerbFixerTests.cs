// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for SlashVerbFixer — deterministic post-processor that
/// replaces slash-stacked verbs/nouns with "or" phrasing per Microsoft
/// style guide. Fixes Acrolinx "Avoid using slashes between words" rule.
/// </summary>
public class SlashVerbFixerTests
{
    // ── Core replacements: two words ───────────────────────────────

    [Theory]
    [InlineData("Create/provision a new resource", "Create or provision a new resource")]
    [InlineData("Update/modify the configuration", "Update or modify the configuration")]
    [InlineData("Delete/remove the account", "Delete or remove the account")]
    [InlineData("Enable/disable the feature", "Enable or disable the feature")]
    [InlineData("List/display all resources", "List or display all resources")]
    public void Fix_TwoWordSlashPattern_ReplacedWithOr(string input, string expected)
    {
        var result = SlashVerbFixer.Fix(input);
        Assert.Equal(expected, result);
    }

    // ── Core replacements: three words ─────────────────────────────

    [Theory]
    [InlineData("Create/update/delete resources", "Create, update, or delete resources")]
    [InlineData("List/filter/sort the results", "List, filter, or sort the results")]
    [InlineData("Read/write/execute permissions", "Read, write, or execute permissions")]
    public void Fix_ThreeWordSlashPattern_ReplacedWithCommaOr(string input, string expected)
    {
        var result = SlashVerbFixer.Fix(input);
        Assert.Equal(expected, result);
    }

    // ── Case preservation ──────────────────────────────────────────

    [Theory]
    [InlineData("Create/Provision a resource", "Create or Provision a resource")]
    [InlineData("create/provision a resource", "create or provision a resource")]
    [InlineData("CREATE/PROVISION a resource", "CREATE or PROVISION a resource")]
    public void Fix_PreservesOriginalCase(string input, string expected)
    {
        var result = SlashVerbFixer.Fix(input);
        Assert.Equal(expected, result);
    }

    // ── Compound term exclusions ───────────────────────────────────

    [Theory]
    [InlineData("Set read/write permissions")]
    [InlineData("Configure input/output settings")]
    [InlineData("Use client/server architecture")]
    [InlineData("Set to true/false")]
    [InlineData("Answer yes/no")]
    [InlineData("Toggle on/off")]
    public void Fix_CompoundTerms_NotReplaced(string input)
    {
        var result = SlashVerbFixer.Fix(input);
        Assert.Equal(input, result);
    }

    // ── Path exclusions ────────────────────────────────────────────

    [Theory]
    [InlineData("Path: C:\\Users\\Admin\\file.txt")]
    [InlineData("Use ./scripts/deploy.sh")]
    [InlineData("File at /usr/local/bin/tool")]
    [InlineData("Navigate to ../config/settings")]
    public void Fix_FilePaths_NotReplaced(string input)
    {
        var result = SlashVerbFixer.Fix(input);
        Assert.Equal(input, result);
    }

    // ── URL exclusions ─────────────────────────────────────────────

    [Theory]
    [InlineData("Visit https://learn.microsoft.com/azure/storage")]
    [InlineData("See http://example.com/docs/api")]
    [InlineData("Check www.microsoft.com/azure")]
    public void Fix_Urls_NotReplaced(string input)
    {
        var result = SlashVerbFixer.Fix(input);
        Assert.Equal(input, result);
    }

    // ── Backtick protection ────────────────────────────────────────

    [Fact]
    public void Fix_InsideBackticks_NotReplaced()
    {
        var input = "Use the `create/delete` command for management.";
        var result = SlashVerbFixer.Fix(input);
        Assert.Contains("`create/delete`", result);
    }

    [Fact]
    public void Fix_MultipleBacktickSpans_ProtectedCorrectly()
    {
        var input = "Commands: `list/filter` and `create/update`, or List/Filter resources.";
        var result = SlashVerbFixer.Fix(input);
        Assert.Contains("`list/filter`", result);
        Assert.Contains("`create/update`", result);
        Assert.Contains("List or Filter", result);
    }

    // ── Code block protection ──────────────────────────────────────

    [Fact]
    public void Fix_InsideCodeBlock_NotReplaced()
    {
        var input = @"Text with Create/Provision here.

```bash
# create/delete operations
az storage create/provision --name test
```

More text with Update/Modify outside.";

        var result = SlashVerbFixer.Fix(input);

        // Inside code block should NOT be replaced
        Assert.Contains("# create/delete operations", result);
        Assert.Contains("az storage create/provision --name test", result);

        // Outside code block SHOULD be replaced
        Assert.Contains("Text with Create or Provision here.", result);
        Assert.Contains("More text with Update or Modify outside.", result);
    }

    // ── Idempotency ────────────────────────────────────────────────

    [Theory]
    [InlineData("Create or provision a resource")]
    [InlineData("List, filter, or sort results")]
    [InlineData("Update or modify the configuration")]
    public void Fix_AlreadyOrPhrased_NoChange(string input)
    {
        var result = SlashVerbFixer.Fix(input);
        Assert.Equal(input, result);
    }

    // ── Edge cases ─────────────────────────────────────────────────

    [Fact]
    public void Fix_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", SlashVerbFixer.Fix(""));
        Assert.Equal("", SlashVerbFixer.Fix(null!));
    }

    [Fact]
    public void Fix_NoMatchingPatterns_ReturnsUnchanged()
    {
        var input = "This is a normal sentence about Azure resources.";
        var result = SlashVerbFixer.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_MultipleOccurrences_AllReplaced()
    {
        var input = "Create/provision resources and list/filter results or update/delete items.";
        var result = SlashVerbFixer.Fix(input);
        Assert.Contains("Create or provision", result);
        Assert.Contains("list or filter", result);
        Assert.Contains("update or delete", result);
    }

    [Fact]
    public void Fix_MoreThanThreeWords_NotReplaced()
    {
        // This is likely a path or complex pattern, should be left alone
        var input = "Use word1/word2/word3/word4/word5 pattern";
        var result = SlashVerbFixer.Fix(input);
        Assert.Contains("word1/word2/word3/word4/word5", result);
    }

    [Fact]
    public void Fix_RealWorldExample_StorageNamespace()
    {
        var input = "Create/provision a new Azure Storage account and configure/manage its settings.";
        var result = SlashVerbFixer.Fix(input);
        Assert.Equal("Create or provision a new Azure Storage account and configure or manage its settings.", result);
    }

    [Fact]
    public void Fix_MixedWithCompoundTerms()
    {
        var input = "Set read/write permissions and Create/Delete resources.";
        var result = SlashVerbFixer.Fix(input);
        // read/write should stay (compound term)
        Assert.Contains("read/write", result);
        // Create/Delete should be replaced
        Assert.Contains("Create or Delete", result);
    }

    [Theory]
    [InlineData("TCP/IP connections")]  // compound term - no change
    [InlineData("Check I/O performance")]  // compound term - no change  
    [InlineData("Status: N/A")]  // compound term - no change
    [InlineData("CI/CD pipeline")]  // compound term - no change
    [InlineData("Configure SLA/SLO metrics")]  // compound term - no change
    [InlineData("Set RPO/RTO values")]  // compound term - no change
    [InlineData("RBAC/ABAC policies")]  // compound term - no change
    [InlineData("UDP/TCP protocols")]  // compound term - no change
    public void Fix_AdditionalCompoundTerms_NotReplaced(string input)
    {
        var result = SlashVerbFixer.Fix(input);
        Assert.Equal(input, result);
    }

    // ── and/or compound term ─────────────────────────────────────────

    [Fact]
    public void Fix_AndOr_NotReplaced()
    {
        var input = "Choose and/or configure options.";
        var result = SlashVerbFixer.Fix(input);
        Assert.Contains("and/or", result);
    }

    // ── Relative path protection ─────────────────────────────────────

    [Theory]
    [InlineData("See src/config/settings.json for details")]
    [InlineData("Edit docs/api/reference.md")]
    [InlineData("Check folder/file.txt")]
    public void Fix_RelativePathsWithExtensions_NotReplaced(string input)
    {
        var result = SlashVerbFixer.Fix(input);
        Assert.Equal(input, result);
    }

    // ── Markdown link protection ─────────────────────────────────────

    [Fact]
    public void Fix_MarkdownLinkDestination_NotReplaced()
    {
        var input = "See [API reference](docs/api/reference) for details.";
        var result = SlashVerbFixer.Fix(input);
        Assert.Contains("[API reference](docs/api/reference)", result);
    }

    [Fact]
    public void Fix_MarkdownLinkWithSlashVerb_LinkProtectedProseFixed()
    {
        var input = "See [API docs](docs/api/reference) and Create/Delete resources.";
        var result = SlashVerbFixer.Fix(input);
        Assert.Contains("[API docs](docs/api/reference)", result);
        Assert.Contains("Create or Delete", result);
    }
}
