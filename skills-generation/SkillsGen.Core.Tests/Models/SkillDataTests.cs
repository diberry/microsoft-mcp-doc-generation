using FluentAssertions;
using SkillsGen.Core.Models;
using Xunit;

namespace SkillsGen.Core.Tests.Models;

public class SkillDataTests
{
    [Fact]
    public void SkillData_RequiredProperties_AreSet()
    {
        var skill = new SkillData
        {
            Name = "azure-storage",
            DisplayName = "Azure Storage",
            Description = "Work with Azure Storage services"
        };

        skill.Name.Should().Be("azure-storage");
        skill.DisplayName.Should().Be("Azure Storage");
        skill.Description.Should().Be("Work with Azure Storage services");
    }
}
