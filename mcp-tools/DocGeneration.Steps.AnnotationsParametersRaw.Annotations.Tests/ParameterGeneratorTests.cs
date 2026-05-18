// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using CSharpGenerator.Generators;
using CSharpGenerator.Models;
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Tests ParameterGenerator.BuildRequiredText.
/// Priority: P0 — parameter generation is Step 1 of the pipeline.
/// </summary>
[Collection("StaticState")]
public class ParameterGeneratorTests
{
    // ── BuildRequiredText ──────────────────────────────────────

    [Fact]
    public void BuildRequiredText_Required_ReturnsRequired()
    {
        var result = ParameterGenerator.BuildRequiredText(
            true, "--name", new HashSet<string>());

        Assert.Equal("Required", result);
    }

    [Fact]
    public void BuildRequiredText_Optional_ReturnsOptional()
    {
        var result = ParameterGenerator.BuildRequiredText(
            false, "--name", new HashSet<string>());

        Assert.Equal("Optional", result);
    }

    [Fact]
    public void BuildRequiredText_ConditionalRequired_AppendsStar()
    {
        var conditionals = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--account-name" };
        var result = ParameterGenerator.BuildRequiredText(
            false, "--account-name", conditionals);

        Assert.Equal("Optional*", result);
    }

    [Fact]
    public void BuildRequiredText_ConditionalRequired_Required_AppendsStar()
    {
        var conditionals = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--vault-name" };
        var result = ParameterGenerator.BuildRequiredText(
            true, "--vault-name", conditionals);

        Assert.Equal("Required*", result);
    }

    [Fact]
    public void BuildRequiredText_NotInConditionalSet_NoStar()
    {
        var conditionals = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--other" };
        var result = ParameterGenerator.BuildRequiredText(
            false, "--name", conditionals);

        Assert.Equal("Optional", result);
    }

    [Fact]
    public void BuildRequiredText_EmptyConditionalSet_NoStar()
    {
        var result = ParameterGenerator.BuildRequiredText(
            true, "--name", new HashSet<string>());

        Assert.Equal("Required", result);
    }

    [Fact]
    public void BuildRequiredText_CaseInsensitiveConditionalMatch()
    {
        // Production code creates the HashSet with OrdinalIgnoreCase.
        // Verify that case differences in param name still match.
        var conditionals = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--Account-Name" };
        var result = ParameterGenerator.BuildRequiredText(
            false, "--account-name", conditionals);

        Assert.Equal("Optional*", result);
    }

    [Fact]
    public void BuildRequiredText_CaseSensitiveSet_DoesNotMatch()
    {
        // When caller uses default (ordinal) comparer, case mismatch → no star.
        // Documents that correctness depends on the caller's comparer.
        var conditionals = new HashSet<string> { "--Account-Name" };
        var result = ParameterGenerator.BuildRequiredText(
            false, "--account-name", conditionals);

        Assert.Equal("Optional", result);
    }

    [Fact]
    public void BuildParameterManifest_AppliesPromptTableTransforms()
    {
        var options = new List<Option>
        {
            new()
            {
                Name = "--vault-name",
                Required = false,
                Description = "Provide vault name"
            }
        };
        var conditionals = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--vault-name" };

        var manifest = ParameterGenerator.BuildParameterManifest(options, conditionals);

        var parameter = Assert.Single(manifest);
        Assert.Equal("--vault-name", parameter.Name);
        Assert.Equal("Vault name", parameter.DisplayName);
        Assert.False(parameter.Required);
        Assert.Equal("Optional*", parameter.RequiredText);
        Assert.True(parameter.IsConditionalRequired);
        Assert.Equal("Provide vault name.", parameter.Description);
    }

    // ── Common Parameter Filtering (#147) ──────────────────────────

    [Fact]
    public void CommonParameters_IncludeResourceGroup()
    {
        // resource-group is a scoping parameter filtered when optional, kept when required.
        var commonParamsPath = Path.Combine(
            FindProjectRoot(), "mcp-tools", "data", "common-parameters.json");
        var json = File.ReadAllText(commonParamsPath);
        var commonParams = System.Text.Json.JsonSerializer.Deserialize<List<CommonParam>>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(commonParams);
        Assert.Contains(commonParams, p => p.Name == "--resource-group");
    }

    [Fact]
    public void CommonParameters_IncludeSubscription()
    {
        // subscription is a scoping parameter that must be in common-parameters.json
        // so that ParameterFilterHelper filters it when optional but keeps it when required (#276).
        var commonParamsPath = Path.Combine(
            FindProjectRoot(), "mcp-tools", "data", "common-parameters.json");
        var json = File.ReadAllText(commonParamsPath);
        var commonParams = System.Text.Json.JsonSerializer.Deserialize<List<CommonParam>>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(commonParams);
        Assert.Contains(commonParams, p => p.Name == "--subscription");
    }

    [Fact]
    public void CommonParameters_OnlyContainInfrastructureAndScopingParams()
    {
        // Infrastructure params (retry-*, auth-method, tenant) and scoping
        // params (subscription) are the allowed common parameters.
        var commonParamsPath = Path.Combine(
            FindProjectRoot(), "mcp-tools", "data", "common-parameters.json");
        var json = File.ReadAllText(commonParamsPath);
        var commonParams = System.Text.Json.JsonSerializer.Deserialize<List<CommonParam>>(json,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        Assert.NotNull(commonParams);
        var allowedPrefixes = new[] { "--retry-", "--auth-method", "--tenant", "--subscription", "--resource-group" };
        foreach (var param in commonParams)
        {
            Assert.True(
                allowedPrefixes.Any(p => param.Name!.StartsWith(p, StringComparison.OrdinalIgnoreCase)),
                $"Unexpected common parameter '{param.Name}'");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static string FindProjectRoot() =>
        DocGeneration.TestInfrastructure.ProjectRootFinder.FindSolutionRoot();

    // ── Canonical Description Override (#592) ──────────────────────────

    [Fact]
    public void BuildParameterManifest_UsesCanonicalDescription_WhenAvailable()
    {
        var options = new List<Option>
        {
            new() { Name = "--subscription", Required = true, Description = "Subscription" },
            new() { Name = "--custom-param", Required = false, Description = "A custom description" }
        };
        var conditionals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var canonicalDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["--subscription"] = "Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name."
        };

        var manifest = ParameterGenerator.BuildParameterManifest(options, conditionals, canonicalDescriptions);

        Assert.Equal(2, manifest.Count);
        // subscription should use canonical description (not the CLI source "Subscription")
        Assert.Equal(
            "Specifies the Azure subscription to use. Accepts either a subscription ID (GUID) or display name.",
            manifest[0].Description);
        // custom-param should keep its original description (with period added)
        Assert.Equal("A custom description.", manifest[1].Description);
    }

    [Fact]
    public void BuildParameterManifest_FallsBackToSourceDescription_WhenNoCanonical()
    {
        var options = new List<Option>
        {
            new() { Name = "--resource-group", Required = true, Description = "Resource group" }
        };
        var conditionals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // No canonical descriptions provided (null)
        var manifest = ParameterGenerator.BuildParameterManifest(options, conditionals, null);

        Assert.Single(manifest);
        Assert.Equal("Resource group.", manifest[0].Description);
    }

    [Fact]
    public void BuildParameterManifest_EmptyCanonicalDescriptions_FallsBackToSource()
    {
        var options = new List<Option>
        {
            new() { Name = "--subscription", Required = true, Description = "Subscription" }
        };
        var conditionals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var emptyCanonical = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var manifest = ParameterGenerator.BuildParameterManifest(options, conditionals, emptyCanonical);

        Assert.Single(manifest);
        Assert.Equal("Subscription.", manifest[0].Description);
    }

    [Fact]
    public void BuildParameterManifest_CaseInsensitiveLookup_MatchesCanonical()
    {
        var options = new List<Option>
        {
            new() { Name = "--Subscription", Required = true, Description = "Sub" }
        };
        var conditionals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var canonicalDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["--subscription"] = "The Azure subscription."
        };

        var manifest = ParameterGenerator.BuildParameterManifest(options, conditionals, canonicalDescriptions);

        Assert.Single(manifest);
        Assert.Equal("The Azure subscription.", manifest[0].Description);
    }

    [Fact]
    public void BuildParameterManifest_CanonicalWithTrailingPeriod_NoDuplicate()
    {
        var options = new List<Option>
        {
            new() { Name = "--tenant", Required = false, Description = "Tenant" }
        };
        var conditionals = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var canonicalDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["--tenant"] = "The Microsoft Entra ID tenant ID or name."
        };

        var manifest = ParameterGenerator.BuildParameterManifest(options, conditionals, canonicalDescriptions);

        Assert.Single(manifest);
        // Should NOT double-period
        Assert.Equal("The Microsoft Entra ID tenant ID or name.", manifest[0].Description);
        Assert.DoesNotContain("..", manifest[0].Description);
    }

    private sealed class CommonParam
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public string? Description { get; set; }
        public bool IsRequired { get; set; }
    }
}
