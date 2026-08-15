// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace DocGeneration.Core.Shared.Tests;

/// <summary>
/// Group 3 — Coverage evaluator: identity/coverage matrix.
/// Tests that the canonical coverage evaluator correctly classifies verdicts
/// using ONLY manifest-authorized aliases, never generic heuristics.
/// </summary>
public class CanonicalCoverageEvaluatorTests
{
    // ── Concrete verdict ─────────────────────────────────────────────

    [Fact]
    public void EvaluateSingleParameter_ConcreteValue_ReturnsConcrete()
    {
        var param = BuildEntry("account", "Account name",
            displayAliases: ["account-name", "account"],
            placeholderAliases: ["account", "account-name", "account_name"]);
        var index = BuildPlaceholderIndex(param);
        var prompts = new[] { "List keys for account named 'myaccount123'" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        Assert.Equal(CoverageVerdict.Concrete, result.Verdict);
        Assert.NotNull(result.MatchEvidence);
        Assert.NotNull(result.MatchedPromptIndex);
    }

    // ── AuthorizedPlaceholder verdict ────────────────────────────────

    [Fact]
    public void EvaluateSingleParameter_AuthorizedPlaceholder_Account_ReturnsAuthorizedPlaceholder()
    {
        var param = BuildEntry("account", "Account name",
            displayAliases: ["account-name", "account"],
            placeholderAliases: ["account", "account-name", "account_name"]);
        var index = BuildPlaceholderIndex(param);
        var prompts = new[] { "Get key-values from <account>" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        Assert.Equal(CoverageVerdict.AuthorizedPlaceholder, result.Verdict);
        Assert.Equal("account", result.MatchEvidence);
    }

    [Fact]
    public void EvaluateSingleParameter_AuthorizedPlaceholder_AccountName_ReturnsAuthorizedPlaceholder()
    {
        var param = BuildEntry("account", "Account name",
            displayAliases: ["account-name", "account"],
            placeholderAliases: ["account", "account-name", "account_name"]);
        var index = BuildPlaceholderIndex(param);
        var prompts = new[] { "Get values from <account_name> config store" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        Assert.Equal(CoverageVerdict.AuthorizedPlaceholder, result.Verdict);
        Assert.Equal("account_name", result.MatchEvidence);
    }

    // ── THE DECISIVE CASE: app_config_store_name → Missing ───────────

    /// <summary>
    /// The decisive case from AD-030: canonical 'account', displayName 'Account name'.
    /// A placeholder like &lt;app_config_store_name&gt; must be MISSING — the current
    /// heuristic falsely reports it covered via Contains/substring matching.
    /// This test MUST FAIL before the fix and PASS after.
    /// </summary>
    [Fact]
    public void EvaluateSingleParameter_AppConfigStoreName_ReturnsMissing_NotFalsePositiveCovered()
    {
        var param = BuildEntry("account", "Account name",
            displayAliases: ["account-name", "account"],
            placeholderAliases: ["account", "account-name", "account_name"]);
        var index = BuildPlaceholderIndex(param);
        var prompts = new[] { "Get key-values from App Configuration store <app_config_store_name>" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        Assert.Equal(CoverageVerdict.Missing, result.Verdict);
    }

    // ── Generic suffixes NEVER authorize placeholders ────────────────

    [Theory]
    [InlineData("account", "Account name", "<resource_name>")]
    [InlineData("account", "Account name", "<storage_account_text>")]
    [InlineData("account", "Account name", "<account_value>")]
    [InlineData("resource-group", "Resource group", "<group_id>")]
    [InlineData("deployment", "Deployment name", "<deployment_array>")]
    [InlineData("agent", "Agent name", "<agent_name_value>")]
    public void EvaluateSingleParameter_GenericSuffix_NeverAuthorizes(
        string canonical, string displayName, string placeholder)
    {
        var param = BuildEntry(canonical, displayName,
            displayAliases: [canonical],
            placeholderAliases: [canonical, canonical.Replace('-', '_')]);
        var index = BuildPlaceholderIndex(param);
        var prompts = new[] { $"Do something with {placeholder}" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        Assert.Equal(CoverageVerdict.Missing, result.Verdict);
    }

    // ── Contains-substring matches NEVER authorize ───────────────────

    [Fact]
    public void EvaluateSingleParameter_ContainsSubstring_NeverAuthorizes()
    {
        // "account" is a substring of "app_config_account_store" — must not match
        var param = BuildEntry("account", "Account name",
            displayAliases: ["account-name", "account"],
            placeholderAliases: ["account", "account-name", "account_name"]);
        var index = BuildPlaceholderIndex(param);
        var prompts = new[] { "Get config from <app_config_account_store>" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        Assert.Equal(CoverageVerdict.Missing, result.Verdict);
    }

    // ── N-of-M word match NEVER authorizes ───────────────────────────

    [Fact]
    public void EvaluateSingleParameter_NofMWordMatch_NeverAuthorizes()
    {
        // "resource-group" has words "resource" and "group". A placeholder "resource_pool_group"
        // matches 2 of 3 words — must NOT count.
        var param = BuildEntry("resource-group", "Resource group",
            displayAliases: ["resource-group"],
            placeholderAliases: ["resource-group", "resource_group"]);
        var index = BuildPlaceholderIndex(param);
        var prompts = new[] { "Deploy to <resource_pool_group>" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        Assert.Equal(CoverageVerdict.Missing, result.Verdict);
    }

    // ── Morphological/plural variants NEVER authorize ────────────────

    [Fact]
    public void EvaluateSingleParameter_PluralVariant_NeverAuthorizes()
    {
        var param = BuildEntry("account", "Account name",
            displayAliases: ["account-name", "account"],
            placeholderAliases: ["account", "account-name", "account_name"]);
        var index = BuildPlaceholderIndex(param);
        var prompts = new[] { "List <accounts> in subscription" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        Assert.Equal(CoverageVerdict.Missing, result.Verdict);
    }

    // ── Abbreviation expansion NEVER authorizes ──────────────────────

    [Fact]
    public void EvaluateSingleParameter_AbbreviationExpansion_NeverAuthorizes()
    {
        var param = BuildEntry("vmss-name", "Virtual machine scale set (VMSS) name",
            displayAliases: ["vmss-name"],
            placeholderAliases: ["vmss-name", "vmss_name"]);
        var index = BuildPlaceholderIndex(param);
        var prompts = new[] { "Scale <virtual_machine_scale_set_name>" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        Assert.Equal(CoverageVerdict.Missing, result.Verdict);
    }

    // ── Token-aware: substring in prose does NOT match ────────────────

    [Fact]
    public void EvaluateSingleParameter_SubstringInProse_NeverMatches()
    {
        var param = BuildEntry("account", "Account name",
            displayAliases: ["account-name", "account"],
            placeholderAliases: ["account", "account-name", "account_name"]);
        var index = BuildPlaceholderIndex(param);
        // "account" appears as a word in prose, not inside a placeholder
        var prompts = new[] { "Show the account details for the App Configuration service" };

        // Without a concrete value pattern (like 'myaccount123'), bare prose word should not satisfy
        // AuthorizedPlaceholder. It might satisfy Concrete if the evaluator's concrete-match
        // logic recognizes it, but the key assertion is that it's NOT AuthorizedPlaceholder
        // from a non-placeholder context.
        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        // This test asserts that arbitrary prose containing the word "account" does NOT satisfy
        // AuthorizedPlaceholder verdict — only extracted placeholder tokens match.
        Assert.NotEqual(CoverageVerdict.AuthorizedPlaceholder, result.Verdict);
    }

    [Fact]
    public void EvaluateSingleParameter_AliasInsideLongerWord_NeverMatches()
    {
        var param = BuildEntry("agent", "Agent name",
            displayAliases: ["agent"],
            placeholderAliases: ["agent", "agent_name"]);
        var index = BuildPlaceholderIndex(param);
        // "agent" appears inside "subagent" — must not count as a placeholder match
        var prompts = new[] { "Deploy the <subagent_configuration>" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        Assert.Equal(CoverageVerdict.Missing, result.Verdict);
    }

    // ── Ambiguous verdict ────────────────────────────────────────────

    [Fact]
    public void EvaluateSingleParameter_Ambiguous_NeverTreatedAsCovered()
    {
        // This would be caught at load time (PLACEHOLDER_MULTI_BIND), but if evaluation
        // somehow encounters ambiguity, it must be Missing/Ambiguous, never covered.
        var param = BuildEntry("knowledge-base", "Knowledge base",
            displayAliases: ["knowledge-base"],
            placeholderAliases: ["knowledge-base", "knowledge_base", "kb-name"]);
        // Simulate ambiguous index — kb-name maps to TWO canonicals
        var ambiguousIndex = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // kb-name is intentionally NOT mapped (removed due to ambiguity)
            ["knowledge-base"] = "knowledge-base",
            ["knowledge_base"] = "knowledge-base",
        };
        var prompts = new[] { "Search in <kb-name>" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, ambiguousIndex);

        // kb-name is not in the index (removed due to ambiguity), so no match
        Assert.NotEqual(CoverageVerdict.Concrete, result.Verdict);
        Assert.NotEqual(CoverageVerdict.AuthorizedPlaceholder, result.Verdict);
    }

    // ── Unknown placeholders survive as upstream text ─────────────────

    [Fact]
    public void EvaluateSingleParameter_UnknownPlaceholder_DoesNotSatisfyCoverage()
    {
        var param = BuildEntry("deployment", "Deployment name",
            displayAliases: ["deployment-name", "deployment"],
            placeholderAliases: ["deployment", "deployment-name", "deployment_name"]);
        var index = BuildPlaceholderIndex(param);
        // <my_custom_thing> is unknown — it does not satisfy any coverage
        var prompts = new[] { "Deploy <my_custom_thing> to the cloud" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        Assert.Equal(CoverageVerdict.Missing, result.Verdict);
    }

    // ── Full EvaluateParameterCoverage ───────────────────────────────

    [Fact]
    public void EvaluateParameterCoverage_AllRequiredCovered_ReturnsTrue()
    {
        var manifest = BuildManifest(
            "azmcp appconfig kv list", "appconfig",
            BuildEntry("account", "Account name",
                displayAliases: ["account-name", "account"],
                placeholderAliases: ["account", "account-name", "account_name"]));
        var prompts = new[] { "List keys in <account> store" };

        var result = CanonicalCoverageEvaluator.EvaluateParameterCoverage(prompts, manifest);

        Assert.True(result.AllRequiredCovered);
        Assert.Single(result.ParameterResults);
        Assert.Equal(CoverageVerdict.AuthorizedPlaceholder, result.ParameterResults[0].Verdict);
    }

    [Fact]
    public void EvaluateParameterCoverage_MissingRequired_ReturnsFalse()
    {
        var manifest = BuildManifest(
            "azmcp appconfig kv get", "appconfig",
            BuildEntry("account", "Account name",
                displayAliases: ["account-name", "account"],
                placeholderAliases: ["account", "account-name", "account_name"]));
        var prompts = new[] { "Get key-values from App Configuration store <app_config_store_name>" };

        var result = CanonicalCoverageEvaluator.EvaluateParameterCoverage(prompts, manifest);

        Assert.False(result.AllRequiredCovered);
        Assert.Single(result.ParameterResults);
        Assert.Equal(CoverageVerdict.Missing, result.ParameterResults[0].Verdict);
    }

    // ── Normalization matrix ─────────────────────────────────────────

    [Theory]
    [InlineData("--account", "account")]
    [InlineData("Account Name", "account-name")]
    [InlineData("resource_group", "resource-group")]
    [InlineData("Resource Group", "resource-group")]
    [InlineData("auth-method", "auth-method")]
    [InlineData("Authentication method", "authentication-method")]
    [InlineData("Virtual machine scale set (VMSS) name", "virtual-machine-scale-set-vmss-name")]
    [InlineData("  --UPPER_Case  ", "upper-case")]
    public void Normalize_ProducesExpectedOutput(string input, string expected)
    {
        var result = CanonicalParameterNormalizer.Normalize(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_EmptyOrNull_ReturnsEmpty(string? input, string expected)
    {
        var result = CanonicalParameterNormalizer.Normalize(input!);
        Assert.Equal(expected, result);
    }

    // ── Realistic beta.34 corpus identities ──────────────────────────

    [Theory]
    [InlineData("account")]
    [InlineData("eventhub")]
    [InlineData("deployment")]
    [InlineData("resource-group")]
    [InlineData("agent")]
    [InlineData("message")]
    [InlineData("knowledge-base")]
    [InlineData("directory-path")]
    [InlineData("cloud-endpoint-name")]
    [InlineData("testrun-id")]
    [InlineData("test-run-id")]
    [InlineData("old-test-run-id")]
    [InlineData("vmss-name")]
    [InlineData("auth-method")]
    [InlineData("param")]
    [InlineData("hostpool-resource-id")]
    public void EvaluateSingleParameter_RealisticIdentity_ExactPlaceholderMatch_Covered(string canonical)
    {
        var param = BuildEntry(canonical, canonical,
            displayAliases: [canonical],
            placeholderAliases: [canonical, canonical.Replace('-', '_')]);
        var index = BuildPlaceholderIndex(param);
        var prompts = new[] { $"Do something with <{canonical}>" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        Assert.Equal(CoverageVerdict.AuthorizedPlaceholder, result.Verdict);
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private static CanonicalParameterEntry BuildEntry(
        string canonicalName, string displayName,
        string[] displayAliases, string[] placeholderAliases,
        bool required = true)
    {
        return new CanonicalParameterEntry(
            canonicalName, displayName, displayAliases, placeholderAliases,
            required, "Required", false, "Test description.");
    }

    private static IReadOnlyDictionary<string, string> BuildPlaceholderIndex(
        CanonicalParameterEntry param)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in param.PlaceholderAliases)
        {
            dict.TryAdd(alias, param.CanonicalName);
        }
        return dict;
    }

    private static CanonicalParameterManifest BuildManifest(
        string command, string ns, params CanonicalParameterEntry[] parameters)
    {
        return new CanonicalParameterManifest(
            "2.0", command, ns,
            new ManifestSourceIdentity("1.0.0", "2026-01-01T00:00:00Z"),
            parameters,
            BuildFullPlaceholderIndex(parameters));
    }

    private static IReadOnlyDictionary<string, string> BuildFullPlaceholderIndex(
        IReadOnlyList<CanonicalParameterEntry> parameters)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parameters)
        {
            foreach (var alias in p.PlaceholderAliases)
            {
                dict.TryAdd(alias, p.CanonicalName);
            }
        }
        return dict;
    }
}
