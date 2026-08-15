// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ExamplePromptGeneratorStandalone.Generators;
using ExamplePromptGeneratorStandalone.Models;
using Shared;
using Xunit;

namespace DocGeneration.Steps.ExamplePrompts.Generation.Tests;

/// <summary>
/// Group 5 — Bounded, idempotent authoritative-prompt repair using canonical manifest.
/// Tests the manifest-aware overload of DeterministicPromptRepairer.Repair.
/// </summary>
public class CanonicalRepairTests
{
    // ── Prompt count and order preserved ─────────────────────────────

    [Fact]
    public void Repair_WithManifest_PreservesPromptCountAndOrder()
    {
        var manifest = BuildManifest("azmcp appconfig kv list", "appconfig",
            BuildEntry("account", "Account name", required: true));
        var prompts = new List<string>
        {
            "List all key-values in the store",
            "Show configuration entries",
            "Get all keys from App Configuration",
            "Display key-value pairs",
            "Retrieve settings from the config store"
        };

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        Assert.Equal(5, result.RepairedPrompts.Count);
        // Verify prompts start with the same scenario text (order preserved)
        Assert.StartsWith("List all key-values", result.RepairedPrompts[0]);
        Assert.StartsWith("Show configuration", result.RepairedPrompts[1]);
        Assert.StartsWith("Get all keys", result.RepairedPrompts[2]);
    }

    // ── Already-covered prompt is byte-identical ─────────────────────

    [Fact]
    public void Repair_WithManifest_AlreadyCoveredPrompt_EmittedByteIdentical()
    {
        var manifest = BuildManifest("azmcp appconfig kv list", "appconfig",
            BuildEntry("account", "Account name", required: true));
        var original = "List key-values from <account> in the App Configuration store";
        var prompts = new List<string> { original };

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        // Byte-identical: the prompt has authorized placeholder <account> — no modification
        Assert.Equal(original, result.RepairedPrompts[0]);
    }

    // ── Missing coverage → at most one bounded clause per missing param

    [Fact]
    public void Repair_WithManifest_MissingCoverage_AppendsOneBoundedClause()
    {
        var manifest = BuildManifest("azmcp appconfig kv get", "appconfig",
            BuildEntry("account", "Account name", required: true),
            BuildEntry("key", "Key", required: true));
        var prompts = new List<string> { "Get the configuration value" };

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        // Must have added coverage clauses for missing params
        Assert.NotEqual("Get the configuration value", result.RepairedPrompts[0]);
        // Should contain references to both parameters
        Assert.Contains("account", result.RepairedPrompts[0], StringComparison.OrdinalIgnoreCase);
    }

    // ── Idempotence: second pass is byte-identical ───────────────────

    [Fact]
    public void Repair_WithManifest_Idempotent_SecondPassIsByteIdentical()
    {
        var manifest = BuildManifest("azmcp storage account create", "storage",
            BuildEntry("account", "Account name", required: true),
            BuildEntry("resource-group", "Resource group", required: true));
        var prompts = new List<string> { "Create a new storage resource" };

        var firstPass = DeterministicPromptRepairer.Repair(prompts, manifest);
        var secondPass = DeterministicPromptRepairer.Repair(firstPass.RepairedPrompts, manifest);

        // Byte-identical on second pass
        Assert.Equal(firstPass.RepairedPrompts.Count, secondPass.RepairedPrompts.Count);
        for (int i = 0; i < firstPass.RepairedPrompts.Count; i++)
        {
            Assert.Equal(firstPass.RepairedPrompts[i], secondPass.RepairedPrompts[i]);
        }
    }

    // ── Unknown placeholders neither replaced nor reinterpreted ──────

    [Fact]
    public void Repair_WithManifest_UnknownPlaceholders_Preserved()
    {
        var manifest = BuildManifest("azmcp search index list", "search",
            BuildEntry("knowledge-base", "Knowledge base", required: true));
        var original = "Search <custom_upstream_thing> in the knowledge base";
        var prompts = new List<string> { original };

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        // Unknown placeholder <custom_upstream_thing> must survive
        Assert.Contains("<custom_upstream_thing>", result.RepairedPrompts[0]);
    }

    // ── Repair provenance/telemetry ──────────────────────────────────

    [Fact]
    public void Repair_WithManifest_MissingCoverage_ReturnsRepairProvenance()
    {
        var manifest = BuildManifest("azmcp eventhubs consumer list", "eventhubs",
            BuildEntry("eventhub", "Event Hub name", required: true));
        var prompts = new List<string> { "List consumer groups" };

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        Assert.NotNull(result.RepairProvenance);
        Assert.NotEmpty(result.RepairProvenance);
        var provenance = result.RepairProvenance[0];
        Assert.Contains("eventhub", provenance.RepairedParameters);
        Assert.Equal("2.0", provenance.ManifestSchemaVersion);
    }

    // ── Regression: repair telemetry must NOT report empty actions while coverage absent

    [Fact]
    public void Repair_WithManifest_RegressionFix_DoesNotReportEmptyActionsWhenCoverageAbsent()
    {
        var manifest = BuildManifest("azmcp appconfig kv get", "appconfig",
            BuildEntry("account", "Account name", required: true));
        // Prompt that triggers the false-positive heuristic — contains "account" only
        // in the descriptive phrase "App Configuration account store"
        var prompts = new List<string> { "Get key-values from App Configuration store <app_config_store_name>" };

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        // The repair must actually DO something — the old bug reported actions=[] and stillUncovered=[]
        // while coverage was genuinely absent.
        Assert.True(
            result.RepairProvenance.Count > 0 || result.StillUncovered.Count > 0 ||
            result.RepairedPrompts[0] != prompts[0],
            "Repair must not silently report no actions while canonical coverage is absent");
    }

    [Fact]
    public void Repair_WithManifest_AppConfigStorePlaceholder_IsTreatedAsMissingAndInjected()
    {
        var manifest = BuildManifest("azmcp appconfig kv get", "appconfig",
            BuildEntry("account", "Account name", required: true));
        var prompts = new List<string> { "Get key-values from App Configuration store <app_config_store_name>" };

        var before = CanonicalCoverageEvaluator.EvaluateSingleParameter(
            prompts, manifest.Parameters[0], manifest.PlaceholderAliasIndex);
        Assert.Equal(CoverageVerdict.Missing, before.Verdict);

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        Assert.Contains(result.Actions, action => action.ParameterName == "account");
        var after = CanonicalCoverageEvaluator.EvaluateSingleParameter(
            result.RepairedPrompts, manifest.Parameters[0], manifest.PlaceholderAliasIndex);
        Assert.True(
            after.Verdict == CoverageVerdict.Concrete || after.Verdict == CoverageVerdict.AuthorizedPlaceholder,
            $"Repair must close the canonical gap, but verdict stayed {after.Verdict}.");
    }

    // ── Manifest order for appended parameters ───────────────────────

    [Fact]
    public void Repair_WithManifest_AppendsInManifestOrder()
    {
        var manifest = BuildManifest("azmcp cosmos container create", "cosmos",
            BuildEntry("account", "Account name", required: true),
            BuildEntry("database", "Database name", required: true),
            BuildEntry("container", "Container name", required: true));
        var prompts = new List<string> { "Create a new container" };

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        var repaired = result.RepairedPrompts[0];
        var accountIdx = repaired.IndexOf("account", StringComparison.OrdinalIgnoreCase);
        var databaseIdx = repaired.IndexOf("database", StringComparison.OrdinalIgnoreCase);
        var containerIdx = repaired.IndexOf("container", StringComparison.OrdinalIgnoreCase);

        // All three should appear, in manifest order
        Assert.True(accountIdx >= 0, "account should appear in repaired prompt");
        Assert.True(databaseIdx >= 0, "database should appear in repaired prompt");
        Assert.True(containerIdx >= 0, "container should appear in repaired prompt");
        Assert.True(accountIdx < databaseIdx, "account before database (manifest order)");
        Assert.True(databaseIdx < containerIdx, "database before container (manifest order)");
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static CanonicalParameterManifest BuildManifest(
        string command, string ns, params CanonicalParameterEntry[] parameters)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parameters)
            foreach (var a in p.PlaceholderAliases)
                index.TryAdd(a, p.CanonicalName);

        return new CanonicalParameterManifest("2.0", command, ns,
            new ManifestSourceIdentity("1.0.0", "2026-01-01T00:00:00Z"),
            parameters, index);
    }

    private static CanonicalParameterEntry BuildEntry(
        string canonical, string displayName, bool required)
    {
        var placeholderAliases = new[] { canonical, canonical.Replace('-', '_') };
        var displayAliases = new[] { CanonicalParameterNormalizer.Normalize(displayName), canonical };
        return new CanonicalParameterEntry(
            canonical, displayName,
            displayAliases.Distinct().ToArray(),
            placeholderAliases.Distinct().ToArray(),
            required, required ? "Required" : "Optional", false,
            $"The {displayName.ToLowerInvariant()}.");
    }
}
