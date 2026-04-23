using SkillsGen.Core.Models;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Models;

public class SkillDataShortDescriptionTests
{
    [Fact]
    public void ShortDescription_FullDescription_ReturnsFirstTwoSentences()
    {
        // Arrange
        var skill = new SkillData
        {
            Name = "test-skill",
            DisplayName = "Test Skill",
            Description = "This is the first sentence. This is the second sentence. This is the third sentence. This is the fourth sentence."
        };

        // Act
        var shortDesc = skill.ShortDescription;

        // Assert
        Assert.Equal("This is the first sentence. This is the second sentence.", shortDesc);
    }

    [Fact]
    public void ShortDescription_WithWhenMarker_TruncatesBeforeMarker()
    {
        // Arrange
        var skill = new SkillData
        {
            Name = "test-skill",
            DisplayName = "Test Skill",
            Description = "This is a description of the skill. It provides many capabilities. WHEN: you need to do something specific."
        };

        // Act
        var shortDesc = skill.ShortDescription;

        // Assert
        Assert.DoesNotContain("WHEN:", shortDesc);
        Assert.StartsWith("This is a description", shortDesc);
    }

    [Fact]
    public void ShortDescription_WithDoNotUseForMarker_TruncatesBeforeMarker()
    {
        // Arrange
        var skill = new SkillData
        {
            Name = "test-skill",
            DisplayName = "Test Skill",
            Description = "This skill helps manage resources. It provides automation. DO NOT USE FOR: manual operations."
        };

        // Act
        var shortDesc = skill.ShortDescription;

        // Assert
        Assert.DoesNotContain("DO NOT USE FOR", shortDesc);
        Assert.Contains("This skill helps manage resources", shortDesc);
    }

    [Fact]
    public void ShortDescription_ShortDescription_ReturnsAsIs()
    {
        // Arrange
        var skill = new SkillData
        {
            Name = "test-skill",
            DisplayName = "Test Skill",
            Description = "Short description."
        };

        // Act
        var shortDesc = skill.ShortDescription;

        // Assert
        Assert.Equal("Short description.", shortDesc);
    }

    [Fact]
    public void ShortDescription_EmptyDescription_ReturnsEmpty()
    {
        // Arrange
        var skill = new SkillData
        {
            Name = "test-skill",
            DisplayName = "Test Skill",
            Description = ""
        };

        // Act
        var shortDesc = skill.ShortDescription;

        // Assert
        Assert.Equal("", shortDesc);
    }

    [Fact]
    public void ShortDescription_LongDescription_TruncatesAt200Chars()
    {
        // Arrange
        var longText = new string('a', 250) + ".";
        var skill = new SkillData
        {
            Name = "test-skill",
            DisplayName = "Test Skill",
            Description = longText
        };

        // Act
        var shortDesc = skill.ShortDescription;

        // Assert
        Assert.True(shortDesc.Length <= 200);
        Assert.EndsWith("...", shortDesc);
    }

    [Fact]
    public void ShortDescription_OneSentence_ReturnsOneSentence()
    {
        // Arrange
        var skill = new SkillData
        {
            Name = "test-skill",
            DisplayName = "Test Skill",
            Description = "This is a single sentence description."
        };

        // Act
        var shortDesc = skill.ShortDescription;

        // Assert
        Assert.Equal("This is a single sentence description.", shortDesc);
    }
}
