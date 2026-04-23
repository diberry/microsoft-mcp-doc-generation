using SkillsGen.Core.Parsers;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Parsers;

public class ChangelogParserTests
{
    [Fact]
    public void Parse_ValidChangelog_ExtractsVersionAndDate()
    {
        var changelog = @"# Changelog

## [1.0.1] - 2026-03-13

### Added

- `azure-upgrade` — Assess and upgrade Azure workloads.

## [1.0.0] - 2025-03-12

### Added

- Initial release.
";

        var entries = ChangelogParser.Parse(changelog);

        Assert.Equal(2, entries.Count);
        Assert.Equal("1.0.1", entries[0].Version);
        Assert.Equal(new DateOnly(2026, 3, 13), entries[0].ReleaseDate);
        Assert.Equal("1.0.0", entries[1].Version);
        Assert.Equal(new DateOnly(2025, 3, 12), entries[1].ReleaseDate);
    }

    [Fact]
    public void Parse_SkillsAdded_ExtractsSkillNames()
    {
        var changelog = @"## [1.0.1] - 2026-03-13

### Added

- `azure-upgrade` — Assess and upgrade Azure workloads.
- `azure-cost` — Cost optimization.
- `microsoft-foundry` — Foundry management.
";

        var entries = ChangelogParser.Parse(changelog);

        Assert.Single(entries);
        Assert.Equal(3, entries[0].SkillsAdded.Count);
        Assert.Contains("azure-upgrade", entries[0].SkillsAdded);
        Assert.Contains("azure-cost", entries[0].SkillsAdded);
        Assert.Contains("microsoft-foundry", entries[0].SkillsAdded);
    }

    [Fact]
    public void Parse_SkillsRemoved_ExtractsSkillNames()
    {
        var changelog = @"## [1.0.1] - 2026-03-13

### Removed

- `azure-old-skill` — Deprecated skill.
";

        var entries = ChangelogParser.Parse(changelog);

        Assert.Single(entries);
        Assert.Single(entries[0].SkillsRemoved);
        Assert.Contains("azure-old-skill", entries[0].SkillsRemoved);
    }

    [Fact]
    public void Parse_SkillsChanged_ExtractsSkillNames()
    {
        var changelog = @"## [1.0.1] - 2026-03-13

### Changed

- Updated `azure-diagnostics` description.
- Updated `microsoft-foundry` and bumped to version 1.0.5.
";

        var entries = ChangelogParser.Parse(changelog);

        Assert.Single(entries);
        Assert.Equal(2, entries[0].SkillsChanged.Count);
        Assert.Contains("azure-diagnostics", entries[0].SkillsChanged);
        Assert.Contains("microsoft-foundry", entries[0].SkillsChanged);
    }

    [Fact]
    public void Parse_MixedSkillsAndNonSkills_OnlyExtractsSkills()
    {
        var changelog = @"## [1.0.1] - 2026-03-13

### Added

- `azure-upgrade` — New skill.

### Changed

- Removed `foundry-mcp` HTTP server from `.mcp.json` (non-spec).
- Updated `azure-diagnostics` description.
";

        var entries = ChangelogParser.Parse(changelog);

        Assert.Single(entries);
        Assert.Single(entries[0].SkillsAdded);
        Assert.Contains("azure-upgrade", entries[0].SkillsAdded);
        Assert.Single(entries[0].SkillsChanged);
        Assert.Contains("azure-diagnostics", entries[0].SkillsChanged);
        // foundry-mcp should not be extracted (doesn't match skill naming pattern)
    }

    [Fact]
    public void Parse_RealChangelog_ExtractsCorrectly()
    {
        var changelog = @"# Changelog

## [1.0.1] - 2026-03-13

### Added

- `azure-upgrade` — Assess and upgrade Azure workloads between plans, tiers, or SKUs.

### Changed

- Removed `foundry-mcp` HTTP server from `.mcp.json` (non-spec `type`/`url` fields).
- Updated `azure-diagnostics` description.
- Updated `microsoft-foundry` description and bumped to version 1.0.5.

## [1.0.0] - 2025-03-12

### Added

- 21 agent skills:
  - `appinsights-instrumentation` — Azure Application Insights telemetry setup.
  - `azure-cost-optimization` — Cost savings analysis and recommendations.
";

        var entries = ChangelogParser.Parse(changelog);

        Assert.Equal(2, entries.Count);
        
        // Version 1.0.1
        Assert.Single(entries[0].SkillsAdded);
        Assert.Contains("azure-upgrade", entries[0].SkillsAdded);
        Assert.Equal(2, entries[0].SkillsChanged.Count);
        Assert.Contains("azure-diagnostics", entries[0].SkillsChanged);
        Assert.Contains("microsoft-foundry", entries[0].SkillsChanged);
        
        // Version 1.0.0
        Assert.True(entries[1].SkillsAdded.Count >= 2);
        Assert.Contains("appinsights-instrumentation", entries[1].SkillsAdded);
        Assert.Contains("azure-cost-optimization", entries[1].SkillsAdded);
    }

    [Fact]
    public void Parse_EmptyChangelog_ReturnsEmptyList()
    {
        var entries = ChangelogParser.Parse("");
        Assert.Empty(entries);
    }

    [Fact]
    public void Parse_NoDuplicateSkills_InSameSection()
    {
        var changelog = @"## [1.0.1] - 2026-03-13

### Added

- `azure-upgrade` — First mention.
- Something about `azure-upgrade` again.
";

        var entries = ChangelogParser.Parse(changelog);

        Assert.Single(entries);
        Assert.Single(entries[0].SkillsAdded);
        Assert.Contains("azure-upgrade", entries[0].SkillsAdded);
    }
}
