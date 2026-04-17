// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace DocGeneration.Steps.Bootstrap.BrandMappings.Tests;

/// <summary>
/// Tests for MergeGroupValidator — validates merge group configuration
/// in brand-to-server-mapping.json per AD-011.
/// </summary>
public class MergeGroupValidatorTests
{
    // ── Valid configurations ────────────────────────────────────────

    [Fact]
    public void Validate_NoMergeGroups_ReturnsValid()
    {
        var mappings = new List<BrandMapping>
        {
            new() { McpServerName = "storage", FileName = "azure-storage" },
            new() { McpServerName = "cosmos", FileName = "azure-cosmos-db" }
        };

        var errors = MergeGroupValidator.Validate(mappings);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ValidMergeGroup_ReturnsValid()
    {
        var mappings = new List<BrandMapping>
        {
            new() { McpServerName = "monitor", FileName = "azure-monitor",
                     MergeGroup = "azure-monitor", MergeOrder = 1, MergeRole = "primary" },
            new() { McpServerName = "workbooks", FileName = "azure-workbooks",
                     MergeGroup = "azure-monitor", MergeOrder = 2, MergeRole = "secondary" },
            new() { McpServerName = "storage", FileName = "azure-storage" }
        };

        var errors = MergeGroupValidator.Validate(mappings);
        Assert.Empty(errors);
    }

    // ── Missing primary ─────────────────────────────────────────────

    [Fact]
    public void Validate_GroupWithNoPrimary_ReturnsError()
    {
        var mappings = new List<BrandMapping>
        {
            new() { McpServerName = "monitor", FileName = "azure-monitor",
                     MergeGroup = "azure-monitor", MergeOrder = 1, MergeRole = "secondary" },
            new() { McpServerName = "workbooks", FileName = "azure-workbooks",
                     MergeGroup = "azure-monitor", MergeOrder = 2, MergeRole = "secondary" }
        };

        var errors = MergeGroupValidator.Validate(mappings);
        Assert.Single(errors);
        Assert.Contains("primary", errors[0], StringComparison.OrdinalIgnoreCase);
    }

    // ── Multiple primaries ──────────────────────────────────────────

    [Fact]
    public void Validate_GroupWithMultiplePrimaries_ReturnsError()
    {
        var mappings = new List<BrandMapping>
        {
            new() { McpServerName = "monitor", FileName = "azure-monitor",
                     MergeGroup = "azure-monitor", MergeOrder = 1, MergeRole = "primary" },
            new() { McpServerName = "workbooks", FileName = "azure-workbooks",
                     MergeGroup = "azure-monitor", MergeOrder = 2, MergeRole = "primary" }
        };

        var errors = MergeGroupValidator.Validate(mappings);
        Assert.Single(errors);
        Assert.Contains("multiple", errors[0], StringComparison.OrdinalIgnoreCase);
    }

    // ── Incomplete merge fields ─────────────────────────────────────

    [Fact]
    public void Validate_MergeGroupWithoutOrder_ReturnsError()
    {
        var mappings = new List<BrandMapping>
        {
            new() { McpServerName = "monitor", FileName = "azure-monitor",
                     MergeGroup = "azure-monitor", MergeRole = "primary" }
        };

        var errors = MergeGroupValidator.Validate(mappings);
        Assert.Single(errors);
        Assert.Contains("mergeOrder", errors[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_MergeGroupWithoutRole_ReturnsError()
    {
        var mappings = new List<BrandMapping>
        {
            new() { McpServerName = "monitor", FileName = "azure-monitor",
                     MergeGroup = "azure-monitor", MergeOrder = 1 }
        };

        var errors = MergeGroupValidator.Validate(mappings);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Contains("mergeRole", StringComparison.OrdinalIgnoreCase));
    }

    // ── Duplicate merge order ───────────────────────────────────────

    [Fact]
    public void Validate_DuplicateMergeOrder_ReturnsError()
    {
        var mappings = new List<BrandMapping>
        {
            new() { McpServerName = "monitor", FileName = "azure-monitor",
                     MergeGroup = "azure-monitor", MergeOrder = 1, MergeRole = "primary" },
            new() { McpServerName = "workbooks", FileName = "azure-workbooks",
                     MergeGroup = "azure-monitor", MergeOrder = 1, MergeRole = "secondary" }
        };

        var errors = MergeGroupValidator.Validate(mappings);
        Assert.Single(errors);
        Assert.Contains("duplicate", errors[0], StringComparison.OrdinalIgnoreCase);
    }

    // ── Invalid role value ──────────────────────────────────────────

    [Fact]
    public void Validate_InvalidMergeRole_ReturnsError()
    {
        var mappings = new List<BrandMapping>
        {
            new() { McpServerName = "monitor", FileName = "azure-monitor",
                     MergeGroup = "azure-monitor", MergeOrder = 1, MergeRole = "leader" }
        };

        var errors = MergeGroupValidator.Validate(mappings);
        Assert.Contains(errors, e => e.Contains("mergeRole", StringComparison.OrdinalIgnoreCase));
    }

    // ── Production config test ──────────────────────────────────────

    [Fact]
    public void Validate_ProductionBrandMapping_IsValid()
    {
        // Verify the actual brand-to-server-mapping.json is valid
        var mappingPath = Path.Combine(
            FindProjectRoot(), "mcp-tools", "data", "brand-to-server-mapping.json");
        var json = File.ReadAllText(mappingPath);
        var mappings = System.Text.Json.JsonSerializer.Deserialize<List<BrandMapping>>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(mappings);
        var errors = MergeGroupValidator.Validate(mappings);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_ProductionConfig_HasMonitorWorkbooksGroup()
    {
        var mappingPath = Path.Combine(
            FindProjectRoot(), "mcp-tools", "data", "brand-to-server-mapping.json");
        var json = File.ReadAllText(mappingPath);
        var mappings = System.Text.Json.JsonSerializer.Deserialize<List<BrandMapping>>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(mappings);
        var monitorGroup = mappings.Where(m => m.MergeGroup == "azure-monitor").ToList();
        Assert.Equal(2, monitorGroup.Count);
        Assert.Single(monitorGroup, m => m.MergeRole == "primary" && m.McpServerName == "monitor");
        Assert.Single(monitorGroup, m => m.MergeRole == "secondary" && m.McpServerName == "workbooks");
    }

    private static string FindProjectRoot() =>
        DocGeneration.TestInfrastructure.ProjectRootFinder.FindSolutionRoot();
}
