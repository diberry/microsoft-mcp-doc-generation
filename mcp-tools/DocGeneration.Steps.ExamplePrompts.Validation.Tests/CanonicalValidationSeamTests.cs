// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace DocGeneration.Steps.ExamplePrompts.Validation.Tests;

/// <summary>
/// Group 4 — Step 2 validation seam: CodeBasedPromptValidator must use
/// CanonicalCoverageEvaluator verdicts instead of the old Covered||PlaceholderDetected.
/// </summary>
public class CanonicalValidationSeamTests
{
    /// <summary>
    /// Proves that a prompt with an unauthorized placeholder (app_config_store_name)
    /// for canonical 'account' is evaluated as Missing by the evaluator,
    /// meaning the validator should flag it as invalid.
    /// </summary>
    [Fact]
    public void Evaluator_UnauthorizedPlaceholder_ReturnsMissing_ValidatorMustFlag()
    {
        var param = new CanonicalParameterEntry(
            "account", "Account name",
            new[] { "account-name", "account" },
            new[] { "account", "account-name", "account_name" },
            true, "Required", false, "The App Configuration account.");
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["account"] = "account",
            ["account-name"] = "account",
            ["account_name"] = "account",
        };
        var prompts = new[] { "Get key-values from <app_config_store_name>" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        // Validator must flag this as NOT covered
        Assert.Equal(CoverageVerdict.Missing, result.Verdict);
    }

    /// <summary>
    /// Proves that a prompt with an authorized placeholder IS covered.
    /// </summary>
    [Fact]
    public void Evaluator_AuthorizedPlaceholder_ReturnsCovered_ValidatorMustPass()
    {
        var param = new CanonicalParameterEntry(
            "resource-group", "Resource group",
            new[] { "resource-group" },
            new[] { "resource-group", "resource_group" },
            true, "Required", false, "The resource group.");
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["resource-group"] = "resource-group",
            ["resource_group"] = "resource-group",
        };
        var prompts = new[] { "List VMs in <resource_group> subscription" };

        var result = CanonicalCoverageEvaluator.EvaluateSingleParameter(prompts, param, index);

        Assert.Equal(CoverageVerdict.AuthorizedPlaceholder, result.Verdict);
    }
}
