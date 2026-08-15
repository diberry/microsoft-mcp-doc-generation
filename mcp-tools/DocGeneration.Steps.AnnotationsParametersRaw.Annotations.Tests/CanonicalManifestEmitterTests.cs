// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CSharpGenerator.Generators;
using Shared;
using Xunit;

namespace DocGeneration.Steps.ExamplePrompts.Generation.Tests;

/// <summary>
/// Group 1 — Manifest is sole identity authority + schema/emit.
/// Tests that ParameterGenerator.BuildParameterManifest emits the correct v2 envelope.
/// </summary>
public class CanonicalManifestEmitterTests
{
    // ── v2 envelope schema ───────────────────────────────────────────

    [Fact]
    public void BuildParameterManifest_EmitsV2Envelope_WithSchemaVersion()
    {
        var manifest = ParameterGenerator.BuildParameterManifest(
            "azmcp appconfig account list", "appconfig",
            "3.0.0-beta.34+eec7acccddab1e16be852a3c3b9503cc9adf7538",
            BuildSampleParameters());

        Assert.Equal("2.0", manifest.SchemaVersion);
        Assert.Equal("azmcp appconfig account list", manifest.ToolCommand);
        Assert.Equal("appconfig", manifest.Namespace);
        Assert.Equal("3.0.0-beta.34+eec7acccddab1e16be852a3c3b9503cc9adf7538",
            manifest.SourceIdentity.AzureMcpBuild);
        Assert.NotNull(manifest.SourceIdentity.GeneratedAtUtc);
    }

    // ── Canonical name is identity ───────────────────────────────────

    [Fact]
    public void BuildParameterManifest_CanonicalNameIsCliSwitchStripped()
    {
        var parameters = new[]
        {
            new RawParameterInput("--account", "Account name", true, "Required", false, "The account.")
        };

        var manifest = ParameterGenerator.BuildParameterManifest(
            "azmcp appconfig kv list", "appconfig", "1.0.0", parameters);

        Assert.Single(manifest.Parameters);
        Assert.Equal("account", manifest.Parameters[0].CanonicalName);
        Assert.Equal("Account name", manifest.Parameters[0].DisplayName);
    }

    // ── Alias derivation is deterministic ────────────────────────────

    [Fact]
    public void BuildParameterManifest_AliasDerivedDeterministically_Account()
    {
        var parameters = new[]
        {
            new RawParameterInput("--account", "Account name", true, "Required", false, "The account.")
        };

        var manifest = ParameterGenerator.BuildParameterManifest(
            "azmcp appconfig kv list", "appconfig", "1.0.0", parameters);

        var entry = manifest.Parameters[0];

        // displayAliases per AD-030 §1.3:
        // Normalize("Account name") = "account-name"
        // Normalize("account") = "account"
        // SpaceJoin(Split("account", '-')) = "account"
        // DISTINCT: ["account-name", "account"]
        Assert.Contains("account-name", entry.DisplayAliases);
        Assert.Contains("account", entry.DisplayAliases);

        // placeholderAliases per AD-030 §1.3:
        // canonicalName = "account"
        // Replace("account", '-', '_') = "account"
        // Normalize("Account name") = "account-name"
        // Replace("account-name", '-', '_') = "account_name"
        // DISTINCT: ["account", "account-name", "account_name"]
        Assert.Contains("account", entry.PlaceholderAliases);
        Assert.Contains("account-name", entry.PlaceholderAliases);
        Assert.Contains("account_name", entry.PlaceholderAliases);
    }

    [Fact]
    public void BuildParameterManifest_AliasDerivedDeterministically_ResourceGroup()
    {
        var parameters = new[]
        {
            new RawParameterInput("--resource-group", "Resource group", true, "Required", false, "The RG.")
        };

        var manifest = ParameterGenerator.BuildParameterManifest(
            "azmcp storage account list", "storage", "1.0.0", parameters);

        var entry = manifest.Parameters[0];
        Assert.Equal("resource-group", entry.CanonicalName);
        Assert.Contains("resource-group", entry.PlaceholderAliases);
        Assert.Contains("resource_group", entry.PlaceholderAliases);
    }

    // ── Culture invariant ────────────────────────────────────────────

    [Fact]
    public void BuildParameterManifest_AliasDerivation_CultureInvariant()
    {
        var parameters = new[]
        {
            new RawParameterInput("--İndex", "INDEX Name", true, "Required", false, "Test.")
        };

        var manifest = ParameterGenerator.BuildParameterManifest(
            "azmcp search index list", "search", "1.0.0", parameters);

        // Should use InvariantCulture for lowercasing, not Turkish rules
        var entry = manifest.Parameters[0];
        Assert.DoesNotContain("\u0131", entry.CanonicalName); // no Turkish dotless i
    }

    // ── Emitter collision elimination (C3) ───────────────────────────

    [Fact]
    public void BuildParameterManifest_CollisionElimination_RemovesAmbiguousAliasFromBothOwners()
    {
        // Two parameters where derived aliases would collide: both would get "name"
        var parameters = new[]
        {
            new RawParameterInput("--first-name", "Name", true, "Required", false, "First."),
            new RawParameterInput("--last-name", "Name", true, "Required", false, "Last.")
        };

        var manifest = ParameterGenerator.BuildParameterManifest(
            "azmcp test tool", "test", "1.0.0", parameters);

        // "name" derived as displayAlias for both → collision → removed from BOTH
        var first = manifest.Parameters.First(p => p.CanonicalName == "first-name");
        var last = manifest.Parameters.First(p => p.CanonicalName == "last-name");

        // The colliding alias "name" should be in NEITHER parameter's displayAliases
        Assert.DoesNotContain("name", first.DisplayAliases);
        Assert.DoesNotContain("name", last.DisplayAliases);

        // But canonical names always survive
        Assert.Contains("first-name", first.DisplayAliases.Concat(new[] { first.CanonicalName }));
        Assert.Contains("last-name", last.DisplayAliases.Concat(new[] { last.CanonicalName }));
    }

    [Fact]
    public void BuildParameterManifest_CollisionElimination_AliasShadowsCanonical_RemovedFromOwner()
    {
        // "account" is canonical for param1. Param2's derived alias would also be "account".
        var parameters = new[]
        {
            new RawParameterInput("--account", "Account name", true, "Required", false, "The account."),
            new RawParameterInput("--storage-account", "Account", true, "Required", false, "Storage account.")
        };

        var manifest = ParameterGenerator.BuildParameterManifest(
            "azmcp storage blob list", "storage", "1.0.0", parameters);

        var storageAccount = manifest.Parameters.First(p => p.CanonicalName == "storage-account");
        // "account" shadows the canonical name of the other param → removed from storage-account's aliases
        Assert.DoesNotContain("account", storageAccount.DisplayAliases);
        Assert.DoesNotContain("account", storageAccount.PlaceholderAliases);
    }

    // ── Required fields emitted ──────────────────────────────────────

    [Fact]
    public void BuildParameterManifest_EmitsAllRequiredFields()
    {
        var parameters = new[]
        {
            new RawParameterInput("--deployment", "Deployment name", false, "Optional", true, "The deployment.")
        };

        var manifest = ParameterGenerator.BuildParameterManifest(
            "azmcp foundry deployment list", "foundryextensions", "1.0.0", parameters);

        var entry = manifest.Parameters[0];
        Assert.Equal("deployment", entry.CanonicalName);
        Assert.Equal("Deployment name", entry.DisplayName);
        Assert.NotEmpty(entry.DisplayAliases);
        Assert.NotEmpty(entry.PlaceholderAliases);
        Assert.False(entry.Required);
        Assert.Equal("Optional", entry.RequiredText);
        Assert.True(entry.IsConditionalRequired);
        Assert.Equal("The deployment.", entry.Description);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static RawParameterInput[] BuildSampleParameters()
    {
        return new[]
        {
            new RawParameterInput("--account", "Account name", true, "Required", false, "The App Configuration account.")
        };
    }
}
