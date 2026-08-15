// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared;
using Xunit;

namespace DocGeneration.Core.Shared.Tests;

/// <summary>
/// Runtime-RED addendum tests: these compile against existing types and demonstrate
/// behavioral failures that the canonical implementation must fix.
/// </summary>
public class CanonicalCoverageRuntimeRedTests
{
    /// <summary>
    /// THE DECISIVE CASE (runtime-RED): The existing ParameterCoverageChecker incorrectly
    /// treats &lt;app_config_store_name&gt; as covered for parameter "account" / "Account name"
    /// because its Contains/substring heuristic matches "account" inside the prose or because
    /// generic-suffix stripping reduces "Account name" to "account" and substring matches.
    ///
    /// After the canonical evaluator is implemented, this false positive must be eliminated.
    /// This test MUST FAIL today (proving the bug exists) and PASS after the fix.
    /// </summary>
    [Fact]
    public void AppConfigStoreName_IsNotCoveredForAccount_CurrentHeuristicFalsePositive()
    {
        // Current code: ParameterCoverageChecker.GetConcretePromptCoverage
        // uses Contains, N-of-M word matching, and generic suffix stripping
        var prompts = new[] { "Get key-values from App Configuration store <app_config_store_name>" };

        var coverage = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Account name", 1);

        // ASSERTION: This should NOT be covered (the placeholder is unauthorized).
        // Today's heuristic incorrectly returns Covered=true or PlaceholderDetected=true.
        // This test is expected to FAIL before the fix.
        Assert.False(coverage.Covered, "account/Account name should NOT be covered by <app_config_store_name>");
        Assert.False(coverage.PlaceholderDetected, "<app_config_store_name> should NOT satisfy 'Account name' placeholder detection");
    }

    /// <summary>
    /// Positive control: verifying that &lt;account&gt; IS correctly detected as a placeholder.
    /// This test should PASS both before and after the fix (positive control for the test above).
    /// </summary>
    [Fact]
    public void AccountPlaceholder_IsDetected_PositiveControl()
    {
        var prompts = new[] { "Get key-values from <account>" };
        var coverage = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Account name", 1);

        // The existing heuristic DOES detect <account> for "Account name" — this should pass.
        Assert.True(coverage.Covered || coverage.PlaceholderDetected,
            "<account> should satisfy coverage for 'Account name'");
    }

    /// <summary>
    /// Architecture test: ParameterCrossCheckService currently defines a private
    /// ParameterManifestEntry record. After the fix, it must be removed in favor
    /// of the shared canonical loader. This test verifies the current (broken) state.
    /// </summary>
    [Fact]
    public void ParameterCrossCheckService_PrivateManifestEntry_MustBeRemoved()
    {
        var serviceType = typeof(ToolFamilyCleanup.Services.ParameterCrossCheckService);
        var nestedTypes = serviceType.GetNestedTypes(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

        // Currently this DOES exist (expected to be true today, the test asserts it should NOT exist)
        var hasPrivateManifestEntry = nestedTypes.Any(t => t.Name == "ParameterManifestEntry");

        // This assertion FAILS today (runtime-RED) because the private record exists.
        // After implementation, the private record is removed and this passes.
        Assert.False(hasPrivateManifestEntry,
            "ParameterCrossCheckService should not define a private ParameterManifestEntry — " +
            "it must use the shared CanonicalParameterManifestLoader.");
    }
}
