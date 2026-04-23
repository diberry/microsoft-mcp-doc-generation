using Microsoft.Extensions.Logging;
using NSubstitute;
using SkillsGen.Core.Assessment;
using SkillsGen.Core.Fetchers;
using SkillsGen.Core.Models;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Assessment;

public class InventoryDriftDetectorTests
{
    private readonly ISkillDirectoryLister _mockSkillFetcher;
    private readonly IChangelogFetcher _mockChangelogFetcher;
    private readonly ILogger<InventoryDriftDetector> _mockLogger;
    private readonly InventoryDriftDetector _detector;

    public InventoryDriftDetectorTests()
    {
        _mockSkillFetcher = Substitute.For<ISkillDirectoryLister>();
        _mockChangelogFetcher = Substitute.For<IChangelogFetcher>();
        _mockLogger = Substitute.For<ILogger<InventoryDriftDetector>>();
        
        _detector = new InventoryDriftDetector(
            _mockSkillFetcher,
            _mockChangelogFetcher,
            _mockLogger);
    }

    [Fact]
    public async Task DetectDriftAsync_SkillInUpstreamNotInInventory_ReportsAsMissing()
    {
        // Arrange
        var upstreamSkills = new List<string> { "azure-skill-a", "azure-skill-b", "azure-skill-c" };
        var inventory = new List<SkillInventoryEntry>
        {
            new("azure-skill-a", "Skill A", "Category"),
            new("azure-skill-b", "Skill B", "Category")
        };

        _mockSkillFetcher
            .ListSubdirectoriesAsync("skills", Arg.Any<CancellationToken>())
            .Returns(upstreamSkills);
        
        _mockChangelogFetcher
            .FetchAsync(Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        var report = await _detector.DetectDriftAsync(inventory);

        // Assert
        Assert.Single(report.SkillsInUpstreamNotInInventory);
        Assert.Contains("azure-skill-c", report.SkillsInUpstreamNotInInventory);
        Assert.True(report.HasDrift);
    }

    [Fact]
    public async Task DetectDriftAsync_SkillInInventoryNotInUpstream_ReportsAsRemoved()
    {
        // Arrange
        var upstreamSkills = new List<string> { "azure-skill-a", "azure-skill-b" };
        var inventory = new List<SkillInventoryEntry>
        {
            new("azure-skill-a", "Skill A", "Category"),
            new("azure-skill-b", "Skill B", "Category"),
            new("azure-skill-removed", "Removed Skill", "Category")
        };

        _mockSkillFetcher
            .ListSubdirectoriesAsync("skills", Arg.Any<CancellationToken>())
            .Returns(upstreamSkills);
        
        _mockChangelogFetcher
            .FetchAsync(Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        var report = await _detector.DetectDriftAsync(inventory);

        // Assert
        Assert.Single(report.SkillsInInventoryNotInUpstream);
        Assert.Contains("azure-skill-removed", report.SkillsInInventoryNotInUpstream);
        Assert.True(report.HasDrift);
    }

    [Fact]
    public async Task DetectDriftAsync_NoDrift_ReportsNoDrift()
    {
        // Arrange
        var upstreamSkills = new List<string> { "azure-skill-a", "azure-skill-b" };
        var inventory = new List<SkillInventoryEntry>
        {
            new("azure-skill-a", "Skill A", "Category"),
            new("azure-skill-b", "Skill B", "Category")
        };

        _mockSkillFetcher
            .ListSubdirectoriesAsync("skills", Arg.Any<CancellationToken>())
            .Returns(upstreamSkills);
        
        _mockChangelogFetcher
            .FetchAsync(Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        var report = await _detector.DetectDriftAsync(inventory);

        // Assert
        Assert.Empty(report.SkillsInUpstreamNotInInventory);
        Assert.Empty(report.SkillsInInventoryNotInUpstream);
        Assert.False(report.HasDrift);
    }

    [Fact]
    public async Task DetectDriftAsync_WithChangelog_ParsesEntriesCorrectly()
    {
        // Arrange
        var upstreamSkills = new List<string> { "azure-skill-a" };
        var inventory = new List<SkillInventoryEntry>
        {
            new("azure-skill-a", "Skill A", "Category")
        };
        
        var changelog = @"## [1.0.0] - 2025-03-12

### Added

- `azure-skill-a` — First skill.
";

        _mockSkillFetcher
            .ListSubdirectoriesAsync("skills", Arg.Any<CancellationToken>())
            .Returns(upstreamSkills);
        
        _mockChangelogFetcher
            .FetchAsync(Arg.Any<CancellationToken>())
            .Returns(changelog);

        // Act
        var report = await _detector.DetectDriftAsync(inventory);

        // Assert
        Assert.Single(report.ChangelogEntries);
        Assert.Equal("1.0.0", report.ChangelogEntries[0].Version);
        Assert.Contains("azure-skill-a", report.ChangelogEntries[0].SkillsAdded);
    }

    [Fact]
    public async Task DetectDriftAsync_ChangelogWithNamingDiscrepancy_ReportsDiscrepancy()
    {
        // Arrange
        var upstreamSkills = new List<string> { "azure-cost" };
        var inventory = new List<SkillInventoryEntry>
        {
            new("azure-cost", "Azure Cost", "Category")
        };
        
        var changelog = @"## [1.0.0] - 2025-03-12

### Added

- `azure-cost-optimization` — Cost savings.
";

        _mockSkillFetcher
            .ListSubdirectoriesAsync("skills", Arg.Any<CancellationToken>())
            .Returns(upstreamSkills);
        
        _mockChangelogFetcher
            .FetchAsync(Arg.Any<CancellationToken>())
            .Returns(changelog);

        // Act
        var report = await _detector.DetectDriftAsync(inventory);

        // Assert
        Assert.NotEmpty(report.ChangelogNamingDiscrepancies);
        Assert.Contains(report.ChangelogNamingDiscrepancies, 
            d => d.Contains("azure-cost-optimization") && d.Contains("azure-cost"));
    }

    [Fact]
    public async Task DetectDriftAsync_CaseInsensitive_MatchesSkills()
    {
        // Arrange
        var upstreamSkills = new List<string> { "Azure-Skill-A", "azure-skill-b" };
        var inventory = new List<SkillInventoryEntry>
        {
            new("azure-skill-a", "Skill A", "Category"),
            new("AZURE-SKILL-B", "Skill B", "Category")
        };

        _mockSkillFetcher
            .ListSubdirectoriesAsync("skills", Arg.Any<CancellationToken>())
            .Returns(upstreamSkills);
        
        _mockChangelogFetcher
            .FetchAsync(Arg.Any<CancellationToken>())
            .Returns((string?)null);

        // Act
        var report = await _detector.DetectDriftAsync(inventory);

        // Assert
        Assert.Empty(report.SkillsInUpstreamNotInInventory);
        Assert.Empty(report.SkillsInInventoryNotInUpstream);
        Assert.False(report.HasDrift);
    }
}
