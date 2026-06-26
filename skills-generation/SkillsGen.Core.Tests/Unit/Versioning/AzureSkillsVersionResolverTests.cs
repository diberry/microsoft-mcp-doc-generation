using FluentAssertions;
using SkillsGen.Core.Versioning;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Versioning;

public class AzureSkillsVersionResolverTests
{
    // ApplyVersionSuffix — folder naming -------------------------------------

    [Fact]
    public void ApplyVersionSuffix_WithTrailingSlash_AppendsVersionToFinalSegment()
    {
        var result = AzureSkillsVersionResolver.ApplyVersionSuffix("../generated-skills/", "1.1.72");
        result.Should().Be("../generated-skills-1.1.72");
    }

    [Fact]
    public void ApplyVersionSuffix_WithoutTrailingSlash_AppendsVersion()
    {
        var result = AzureSkillsVersionResolver.ApplyVersionSuffix("../generated-skills", "1.1.72");
        result.Should().Be("../generated-skills-1.1.72");
    }

    [Fact]
    public void ApplyVersionSuffix_WithBackslash_TrimsBeforeAppending()
    {
        var result = AzureSkillsVersionResolver.ApplyVersionSuffix(@"..\generated-skills\", "1.1.72");
        result.Should().Be(@"..\generated-skills-1.1.72");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ApplyVersionSuffix_WithNoVersion_ReturnsUnchanged(string? version)
    {
        var result = AzureSkillsVersionResolver.ApplyVersionSuffix("../generated-skills/", version);
        result.Should().Be("../generated-skills/");
    }

    [Fact]
    public void ApplyVersionSuffix_WithVersionAndTimestamp_AppendsBoth()
    {
        var ts = new DateTimeOffset(2026, 5, 31, 16, 25, 25, TimeSpan.Zero);
        var result = AzureSkillsVersionResolver.ApplyVersionSuffix("../generated-skills/", "1.1.72", ts);
        result.Should().Be("../generated-skills-1.1.72-2026-05-31-162525");
    }

    [Fact]
    public void ApplyVersionSuffix_WithTimestampNoVersion_AppendsTimestampOnly()
    {
        var ts = new DateTimeOffset(2026, 5, 31, 16, 25, 25, TimeSpan.Zero);
        var result = AzureSkillsVersionResolver.ApplyVersionSuffix("../generated-skills/", null, ts);
        result.Should().Be("../generated-skills-2026-05-31-162525");
    }

    [Fact]
    public void ApplyVersionSuffix_WithNoVersionNoTimestamp_ReturnsUnchanged()
    {
        var result = AzureSkillsVersionResolver.ApplyVersionSuffix("../generated-skills/", null, null);
        result.Should().Be("../generated-skills/");
    }

    // ResolveVersion — reads all-up azure-skills plugin.json -----------------

    [Fact]
    public void ResolveVersion_FromPluginJsonInSourcePath_ReturnsVersion()
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "plugin.json"), """{ "name": "azure", "version": "1.1.72" }""");

        AzureSkillsVersionResolver.ResolveVersion(dir).Should().Be("1.1.72");
    }

    [Fact]
    public void ResolveVersion_FromPluginJsonInParent_ReturnsVersion()
    {
        // plugin.json lives at skills-source/plugin.json; source path points at skills-source/skills/
        var root = NewTempDir();
        File.WriteAllText(Path.Combine(root, "plugin.json"), """{ "version": "2.0.0" }""");
        var skillsDir = Path.Combine(root, "skills");
        Directory.CreateDirectory(skillsDir);

        AzureSkillsVersionResolver.ResolveVersion(skillsDir).Should().Be("2.0.0");
    }

    [Fact]
    public void ResolveVersion_WhenPluginJsonMissing_ReturnsNull()
    {
        var dir = NewTempDir();
        AzureSkillsVersionResolver.ResolveVersion(dir).Should().BeNull();
    }

    [Fact]
    public void ResolveVersion_WhenPluginJsonMalformed_ReturnsNull()
    {
        var dir = NewTempDir();
        File.WriteAllText(Path.Combine(dir, "plugin.json"), "{ not valid json");

        AzureSkillsVersionResolver.ResolveVersion(dir).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ResolveVersion_WithNoSourcePath_ReturnsNull(string? sourcePath)
    {
        AzureSkillsVersionResolver.ResolveVersion(sourcePath).Should().BeNull();
    }

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "skillsgen-version-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
