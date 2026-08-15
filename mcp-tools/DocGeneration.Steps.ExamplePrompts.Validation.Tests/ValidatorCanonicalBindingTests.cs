// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace DocGeneration.Steps.ExamplePrompts.Validation.Tests;

/// <summary>
/// Group 3 — CodeBasedPromptValidator canonical seam binding tests.
/// Proves that when a CanonicalParameterManifest is supplied, the validator uses
/// CanonicalCoverageEvaluator (not the old Covered||PlaceholderDetected logic),
/// so that an Ambiguous/unauthorized placeholder is never counted as covered.
/// </summary>
public class ValidatorCanonicalBindingTests
{
    private readonly CodeBasedPromptValidator _validator = new();

    private static CanonicalParameterManifest BuildManifest(string canonicalName, string displayName, params string[] placeholderAliases)
    {
        var entry = new CanonicalParameterEntry(
            canonicalName, displayName,
            new[] { displayName.ToLowerInvariant().Replace(' ', '-'), canonicalName },
            placeholderAliases.Length > 0 ? placeholderAliases : new[] { canonicalName },
            true, "Required", false, $"The {displayName}.");

        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in entry.PlaceholderAliases)
            index[alias] = canonicalName;

        return new CanonicalParameterManifest(
            "2.0", $"azmcp test {canonicalName}", "test",
            new ManifestSourceIdentity("1.0.0", "2026-01-01T00:00:00Z"),
            new[] { entry },
            index);
    }

    /// <summary>
    /// RED binding test: an unauthorized placeholder that WOULD match the old heuristic
    /// (because it contains the parameter name as a substring) must NOT count as covered
    /// when a manifest is provided with strict authorized aliases.
    /// 
    /// Without the fix (legacy path): "vault" ⊂ "vault-url" → PlaceholderDetected=true → covered
    /// With the fix (canonical path): "vault-url" is NOT in authorized aliases → Missing → not covered
    /// </summary>
    [Fact]
    public void ValidatePrompts_WithManifest_UnauthorizedPlaceholder_ReturnsIsValidFalse()
    {
        // "vault" is the canonical parameter; authorized placeholder aliases are ONLY
        // "vault" and "vault-name". The prompt uses <vault-url> which
        // is NOT in the authorized alias list but WOULD match the old heuristic
        // (because "vault-url" contains "vault" as a substring).
        var manifest = BuildManifest("vault", "Vault name", "vault", "vault-name");

        var prompts = new[] { "Get secret from <vault-url> named 'my-secret'" };

        var result = _validator.ValidatePrompts(prompts, manifest);

        // The unauthorized placeholder must NOT be counted as covered
        Assert.False(result.IsValid,
            "An unauthorized placeholder (<vault-url>) must not count as covered when a manifest is supplied. " +
            "Only authorized aliases ('vault', 'vault-name') should match.");
        Assert.Single(result.Details);
        Assert.Equal("vault", result.Details[0].ParameterName);
    }

    /// <summary>
    /// Authorized placeholder must still pass when manifest is provided.
    /// </summary>
    [Fact]
    public void ValidatePrompts_WithManifest_AuthorizedPlaceholder_ReturnsIsValidTrue()
    {
        var manifest = BuildManifest("resource-group", "Resource group", "resource-group", "resource_group");

        var prompts = new[] { "List VMs in <resource_group> named 'my-rg'" };

        var result = _validator.ValidatePrompts(prompts, manifest);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Concrete value mention must pass when manifest is provided.
    /// </summary>
    [Fact]
    public void ValidatePrompts_WithManifest_ConcreteValue_ReturnsIsValidTrue()
    {
        var manifest = BuildManifest("account", "Account name", "account", "account-name", "account_name");

        var prompts = new[] { "List key-values for account 'my-appconfig'" };

        var result = _validator.ValidatePrompts(prompts, manifest);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// After removing the legacy path, all validation requires a manifest.
    /// This test verifies that with a manifest, an authorized placeholder is still covered.
    /// (Replaces old test that verified legacy behavior without a manifest.)
    /// </summary>
    [Fact]
    public void ValidatePrompts_WithManifest_AuthorizedPlaceholder_LegacyPathRemoved()
    {
        var manifest = BuildManifest("vault", "Vault name", "vault", "vault-name");
        var prompts = new[] { "Get secret from vault <vault-name>" };

        var result = _validator.ValidatePrompts(prompts, manifest);

        Assert.True(result.IsValid);
    }
}
