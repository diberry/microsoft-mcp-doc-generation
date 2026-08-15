using System;
using System.Collections.Generic;
using DocGeneration.Steps.ExamplePrompts.Validation;
using Shared;
using Xunit;

namespace DocGeneration.Steps.ExamplePrompts.Validation.Tests;

public class CodeBasedPromptValidatorTests
{
    private readonly CodeBasedPromptValidator _validator = new();

    private static CanonicalParameterManifest BuildManifest(params (string name, bool required)[] parameters)
    {
        var entries = parameters.Select(p => new CanonicalParameterEntry(
            p.name, p.name,
            new[] { p.name },
            new[] { p.name, $"{p.name}-name", p.name.Replace("-", "_") },
            p.required, p.required ? "Required" : "Optional", false, $"The {p.name}.")).ToList();

        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in entries)
            foreach (var alias in e.PlaceholderAliases)
                index.TryAdd(alias, e.CanonicalName);

        return new CanonicalParameterManifest(
            "2.0", "azmcp test tool", "test",
            new ManifestSourceIdentity("1.0.0", "2026-01-01T00:00:00Z"),
            entries, index);
    }

    [Fact]
    public void AllParamsCovered_ReturnsIsValidTrue()
    {
        var prompts = new[]
        {
            "List secrets in vault named 'my-vault' and show key named 'my-key'",
        };
        var manifest = BuildManifest(("vault", true), ("key", true));

        var result = _validator.ValidatePrompts(prompts, manifest);

        Assert.True(result.IsValid);
        Assert.Equal(1, result.TotalPrompts);
        Assert.Equal(2, result.TotalRequiredParameters);
    }

    [Fact]
    public void MissingRequiredParam_ReturnsIsValidFalse()
    {
        var prompts = new[]
        {
            "List all virtual machines",
            "Show VM details",
        };
        var manifest = BuildManifest(("resource-group", true));

        var result = _validator.ValidatePrompts(prompts, manifest);

        Assert.False(result.IsValid);
        var detail = Assert.Single(result.Details);
        Assert.Equal("resource-group", detail.ParameterName);
        Assert.False(detail.Covered);
        Assert.False(detail.PlaceholderDetected);
    }

    [Fact]
    public void PlaceholderOnly_AuthorizedPlaceholder_CoveredViaManifest()
    {
        // Prompt uses "<vault-name>" which is an authorized placeholder alias for "vault".
        // The evaluator may detect "vault" as concrete (word in text) OR as authorized placeholder.
        // Either way, the result must be IsValid=true.
        var prompts = new[] { "Get secret from <vault-name>" };
        var manifest = BuildManifest(("vault", true));

        var result = _validator.ValidatePrompts(prompts, manifest);

        Assert.True(result.IsValid);
        var detail = Assert.Single(result.Details);
        // Either concrete or placeholder is acceptable — both mean "covered"
        Assert.True(detail.Covered || detail.PlaceholderDetected,
            "Authorized placeholder or concrete reference should mark parameter as covered");
    }

    [Fact]
    public void EmptyRequiredParams_ReturnsIsValidTrue()
    {
        var prompts = new[] { "List all resources" };
        // Manifest with only optional params
        var manifest = BuildManifest(("filter", false));

        var result = _validator.ValidatePrompts(prompts, manifest);

        Assert.True(result.IsValid);
        Assert.Equal(0, result.TotalRequiredParameters);
        Assert.Empty(result.Details);
    }

    [Fact]
    public void UnauthorizedPlaceholder_IsNotCovered()
    {
        // "vault-url" is NOT in authorized aliases for "vault"
        var prompts = new[] { "Get secret from <vault-url>" };
        var manifest = BuildManifest(("vault", true));

        var result = _validator.ValidatePrompts(prompts, manifest);

        Assert.False(result.IsValid);
        var detail = Assert.Single(result.Details);
        Assert.False(detail.Covered);
        Assert.False(detail.PlaceholderDetected);
    }

    [Fact]
    public void ConcreteValue_IsCovered()
    {
        var prompts = new[] { "List key-values for account 'my-appconfig'" };
        var manifest = BuildManifest(("account", true));

        var result = _validator.ValidatePrompts(prompts, manifest);

        Assert.True(result.IsValid);
    }
}
