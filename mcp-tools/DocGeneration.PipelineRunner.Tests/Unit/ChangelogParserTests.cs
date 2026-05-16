using PipelineRunner.Services;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class ChangelogParserTests
{
    // ─── ParseSections ────────────────────────────────────────────────────────

    [Fact]
    public void ParseSections_EmptyContent_ReturnsEmpty()
    {
        var sections = ChangelogParser.ParseSections(string.Empty);
        Assert.Empty(sections);
    }

    [Fact]
    public void ParseSections_WhitespaceContent_ReturnsEmpty()
    {
        var sections = ChangelogParser.ParseSections("   \n\n  ");
        Assert.Empty(sections);
    }

    [Fact]
    public void ParseSections_SingleVersionSection_ReturnsSingleSection()
    {
        const string content = """
            # Changelog

            ## [1.2.3] - 2025-05-01
            ### Changed
            - storage: improved reliability
            """;

        var sections = ChangelogParser.ParseSections(content);

        var section = Assert.Single(sections);
        Assert.Equal("1.2.3", section.Version);
        Assert.Contains("storage:", section.Content);
    }

    [Fact]
    public void ParseSections_MultipleVersionSections_ParsesAll()
    {
        const string content = """
            ## [Unreleased]
            - compute: new tool

            ## [1.2.3] - 2025-05-01
            - storage: updated

            ## [1.2.2] - 2025-04-01
            - keyvault: fix
            """;

        var sections = ChangelogParser.ParseSections(content);

        Assert.Equal(3, sections.Count);
        Assert.Equal("Unreleased", sections[0].Version);
        Assert.Equal("1.2.3", sections[1].Version);
        Assert.Equal("1.2.2", sections[2].Version);
    }

    [Fact]
    public void ParseSections_ContentIsScopedToSection()
    {
        const string content = """
            ## [2.0.0]
            - compute: major changes

            ## [1.0.0]
            - storage: first release
            """;

        var sections = ChangelogParser.ParseSections(content);

        Assert.Equal(2, sections.Count);
        Assert.Contains("compute:", sections[0].Content);
        Assert.DoesNotContain("storage:", sections[0].Content);
        Assert.Contains("storage:", sections[1].Content);
        Assert.DoesNotContain("compute:", sections[1].Content);
    }

    // ─── HasMentionOf ─────────────────────────────────────────────────────────

    [Fact]
    public void HasMentionOf_NamespacePresent_ReturnsTrue()
    {
        const string content = "- storage: fixed connection timeout";
        Assert.True(ChangelogParser.HasMentionOf(content, "storage"));
    }

    [Fact]
    public void HasMentionOf_NamespaceAbsent_ReturnsFalse()
    {
        const string content = "- compute: new list command";
        Assert.False(ChangelogParser.HasMentionOf(content, "storage"));
    }

    [Fact]
    public void HasMentionOf_IsCaseInsensitive()
    {
        const string content = "- Storage: improved reliability";
        Assert.True(ChangelogParser.HasMentionOf(content, "storage"));
    }

    [Fact]
    public void HasMentionOf_NamespaceAsSubstring_ReturnsTrue()
    {
        // "storage" appears in "azure-storage-account"
        const string content = "- azmcp azure-storage-account list updated";
        Assert.True(ChangelogParser.HasMentionOf(content, "storage"));
    }

    [Fact]
    public void HasMentionOf_EmptyContent_ReturnsFalse()
    {
        Assert.False(ChangelogParser.HasMentionOf(string.Empty, "storage"));
    }

    [Fact]
    public void HasMentionOf_EmptyNamespace_ReturnsFalse()
    {
        Assert.False(ChangelogParser.HasMentionOf("some content", string.Empty));
    }

    // ─── IsVersionRelevantFor ──────────────────────────────────────────────────

    [Fact]
    public void IsVersionRelevantFor_Unreleased_AlwaysRelevant()
    {
        Assert.True(ChangelogParser.IsVersionRelevantFor("Unreleased", "1.2.3"));
        Assert.True(ChangelogParser.IsVersionRelevantFor("Unreleased", "99.0.0"));
    }

    [Fact]
    public void IsVersionRelevantFor_NewerVersion_ReturnsTrue()
    {
        Assert.True(ChangelogParser.IsVersionRelevantFor("1.2.4", "1.2.3"));
        Assert.True(ChangelogParser.IsVersionRelevantFor("2.0.0", "1.9.9"));
    }

    [Fact]
    public void IsVersionRelevantFor_EqualVersion_ReturnsTrue()
    {
        // We want to check the section that matches the current version
        Assert.True(ChangelogParser.IsVersionRelevantFor("1.2.3", "1.2.3"));
    }

    [Fact]
    public void IsVersionRelevantFor_OlderVersion_ReturnsFalse()
    {
        Assert.False(ChangelogParser.IsVersionRelevantFor("1.2.2", "1.2.3"));
        Assert.False(ChangelogParser.IsVersionRelevantFor("0.9.0", "1.0.0"));
    }

    [Fact]
    public void IsVersionRelevantFor_VersionWithDateSuffix_ParsesCorrectly()
    {
        // CHANGELOG headers often include a date: ## [1.2.3] - 2025-05-01
        // The version string captured is "1.2.3] - 2025-05-01" — but our parser
        // captures only the inner text, e.g. "1.2.3 - 2025-05-01" or "1.2.3".
        // Verify we correctly parse the numeric prefix before a space.
        Assert.True(ChangelogParser.IsVersionRelevantFor("1.2.4 - 2025-05-01", "1.2.3"));
        Assert.False(ChangelogParser.IsVersionRelevantFor("1.2.2 - 2025-03-01", "1.2.3"));
    }

    [Fact]
    public void IsVersionRelevantFor_PreReleaseSuffix_ParsesNumericPart()
    {
        // Pre-release versions like "1.0.0-rc.2.25502.107" strip to "1.0.0"
        Assert.True(ChangelogParser.IsVersionRelevantFor("1.0.0", "1.0.0-rc.2.25502.107"));
        Assert.False(ChangelogParser.IsVersionRelevantFor("0.9.0", "1.0.0-rc.2.25502.107"));
    }

    [Fact]
    public void IsVersionRelevantFor_UnparseableVersion_ReturnsTrue()
    {
        // Conservative: when we can't parse, include the section
        Assert.True(ChangelogParser.IsVersionRelevantFor("not-a-version", "1.2.3"));
        Assert.True(ChangelogParser.IsVersionRelevantFor("1.2.3", "not-a-version"));
    }
}
