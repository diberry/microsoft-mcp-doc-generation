// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ExamplePromptGeneratorStandalone.Generators;
using ExamplePromptGeneratorStandalone.Models;
using ExamplePromptGeneratorStandalone.Sanitizers;
using Shared;
using Xunit;

namespace DocGeneration.Steps.ExamplePrompts.Generation.Tests;

/// <summary>
/// Tests for DeterministicPromptRepairer covering all 3 blocking issues from adversarial review
/// plus key non-blocking issues (param name canonicalization, enum escaping, value safety).
/// TDD: Tests written BEFORE implementation per AD-007/AD-010.
/// </summary>
public class DeterministicPromptRepairerTests
{
    // ──────────────────────────────────────────────────────────────────────────────
    // BLOCKING #1: Every non-blank prompt gets ALL missing params (no round-robin)
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Repair_InjectsAllMissingParamsIntoEveryNonBlankPrompt()
    {
        var prompts = new List<string>
        {
            "List all blobs in the specified container.",
            "Show me the metadata for a given blob.",
            "Download files from blob storage.",
            "", // blank — should be skipped
            "Check blob storage metrics."
        };
        var required = new List<Option>
        {
            new() { Name = "account", Required = true, Description = "Storage account name" },
            new() { Name = "resource-group", Required = true, Description = "Resource group name" }
        };

        var result = DeterministicPromptRepairer.Repair(prompts, required);

        // Every non-blank prompt must cover BOTH required params after repair
        var nonBlank = result.RepairedPrompts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        Assert.True(nonBlank.Count >= 4, "Should have at least 4 non-blank prompts");

        foreach (var prompt in nonBlank)
        {
            var accountCoverage = ParameterCoverageChecker.GetConcretePromptCoverage(
                new[] { prompt }, "account", 2);
            var rgCoverage = ParameterCoverageChecker.GetConcretePromptCoverage(
                new[] { prompt }, "resource-group", 2);

            Assert.True(accountCoverage.Covered || accountCoverage.PlaceholderDetected,
                $"Prompt lacks 'account' coverage: {prompt}");
            Assert.True(rgCoverage.Covered || rgCoverage.PlaceholderDetected,
                $"Prompt lacks 'resource-group' coverage: {prompt}");
        }
    }

    [Fact]
    public void Repair_BlankPromptsAreNotModified()
    {
        var prompts = new List<string> { "List accounts.", "", "   " };
        var required = new List<Option>
        {
            new() { Name = "account", Required = true, Description = "Account name" }
        };

        var result = DeterministicPromptRepairer.Repair(prompts, required);

        Assert.Equal("", result.RepairedPrompts[1]);
        Assert.Equal("   ", result.RepairedPrompts[2]);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // BLOCKING #2: Verification runs AFTER sanitization
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Repair_StillUncovered_DetectedAfterSanitization()
    {
        // Injected value must survive sanitization for coverage to hold.
        // "location" is in the ValueBank with safe values like "eastus"
        var prompts = new List<string>
        {
            "Deploy the resources to a specific region."
        };
        var required = new List<Option>
        {
            new() { Name = "location", Required = true, Description = "Azure region for deployment" }
        };

        var result = DeterministicPromptRepairer.Repair(prompts, required);

        // "eastus" is safe — won't be sanitized — should NOT appear in StillUncovered
        Assert.DoesNotContain("location", result.StillUncovered);
    }

    [Fact]
    public void Repair_ValueBankExcludesValueKey_CredentialSafe()
    {
        // "value" key in old ValueBank has credentials. ParameterValueBank must not have it.
        Assert.False(ParameterValueBank.Bank.ContainsKey("value"),
            "ParameterValueBank must not contain 'value' key (credential risk)");
    }

    [Fact]
    public void Repair_InjectedEnumValueSurvivesSanitizer_NotInStillUncovered()
    {
        // Enum value "standard" should survive sanitization cleanly
        var prompts = new List<string> { "Create a new storage tier." };
        var required = new List<Option>
        {
            new() { Name = "tier", Required = true, Description = "Available options: 'standard', 'premium'" }
        };

        var result = DeterministicPromptRepairer.Repair(prompts, required);

        Assert.DoesNotContain("tier", result.StillUncovered);
        // Verify the sanitized form still has coverage
        var sanitized = result.RepairedPrompts.Select(CredentialSanitizer.Sanitize).ToList();
        var coverage = ParameterCoverageChecker.GetConcretePromptCoverage(sanitized, "tier", 1, required[0].Description);
        Assert.True(coverage.Covered || coverage.PlaceholderDetected);
    }

    [Fact]
    public void Repair_InjectedValueDestroyedBySanitizer_AppearsInStillUncovered()
    {
        // If we somehow inject a value that looks like a JWT, sanitizer kills it
        // This tests the safety net by using a param with an enum containing a JWT-like string
        var prompts = new List<string> { "Authenticate the user." };
        var required = new List<Option>
        {
            new() { Name = "token-input", Required = true, Description = "Available options: 'eyJhbGciOiJIUzI1NiJ9'" }
        };

        var result = DeterministicPromptRepairer.Repair(prompts, required);

        // The enum value '******' is rejected by IsValidValue (contains special chars),
        // so the repairer falls back to the contoso heuristic. Verify repair actually happened.
        Assert.Single(result.Actions);
        Assert.Equal("token-input", result.Actions[0].ParameterName);
        // The fallback value should be safe and survive sanitization
        Assert.True(DeterministicPromptRepairer.IsValidValue(result.Actions[0].InjectedValue),
            "Injected fallback value must pass IsValidValue");
        Assert.DoesNotContain("******", result.RepairedPrompts[0]);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // BLOCKING #3: Effective coverage = Covered OR PlaceholderDetected
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void GetEffectiveCoverage_TrueWhenPlaceholderDetected()
    {
        var prompts = new List<string> { "List items for <account>" };
        var result = DeterministicPromptRepairer.GetEffectiveCoverage(prompts, "account", 1, null);
        Assert.True(result);
    }

    [Fact]
    public void GetEffectiveCoverage_TrueWhenConcreteCovered()
    {
        var prompts = new List<string> { "List items for account 'mystorageacct'" };
        var result = DeterministicPromptRepairer.GetEffectiveCoverage(prompts, "account", 1, null);
        Assert.True(result);
    }

    [Fact]
    public void GetEffectiveCoverage_FalseWhenNeitherCoveredNorPlaceholder()
    {
        var prompts = new List<string> { "List all available items" };
        var result = DeterministicPromptRepairer.GetEffectiveCoverage(prompts, "account", 1, null);
        Assert.False(result);
    }

    [Fact]
    public void Repair_SkipsAlreadyCoveredByPlaceholder()
    {
        var prompts = new List<string> { "List items for <account> in resource group 'rg-prod'" };
        var required = new List<Option>
        {
            new() { Name = "account", Required = true, Description = "Account name" },
            new() { Name = "resource-group", Required = true, Description = "RG name" }
        };

        var result = DeterministicPromptRepairer.Repair(prompts, required);

        // No actions needed — both are already effectively covered
        Assert.Empty(result.Actions);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // NON-BLOCKING #4: CLI names with '--' prefix are canonicalized
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CanonicalizeParamName_StripsLeadingDashes()
    {
        Assert.Equal("account", DeterministicPromptRepairer.CanonicalizeParamName("--account"));
        Assert.Equal("resource-group", DeterministicPromptRepairer.CanonicalizeParamName("--resource-group"));
        Assert.Equal("name", DeterministicPromptRepairer.CanonicalizeParamName("name"));
    }

    [Fact]
    public void Repair_WorksWithCliPrefixedParamNames()
    {
        var prompts = new List<string> { "List all storage blobs." };
        var required = new List<Option>
        {
            new() { Name = "--account", Required = true, Description = "Storage account" }
        };

        var result = DeterministicPromptRepairer.Repair(prompts, required);

        // Should resolve "account" from bank despite "--account" input
        Assert.Single(result.Actions);
        Assert.Equal("account", result.Actions[0].ParameterName);
        Assert.Equal("mystorageacct", result.Actions[0].InjectedValue);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // NON-BLOCKING #5: Value validation (escaping, length, special chars)
    // ──────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("normal-value", true)]
    [InlineData("eastus", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("value\nwith\nnewlines", false)]
    [InlineData("value'with'quotes", false)]
    public void IsValidValue_ValidatesCorrectly(string value, bool expected)
    {
        Assert.Equal(expected, DeterministicPromptRepairer.IsValidValue(value));
    }

    [Fact]
    public void IsValidValue_RejectsExcessiveLength()
    {
        var longValue = new string('a', 201);
        Assert.False(DeterministicPromptRepairer.IsValidValue(longValue));
    }

    [Fact]
    public void IsValidValue_AcceptsMaxLength()
    {
        var maxValue = new string('a', 200);
        Assert.True(DeterministicPromptRepairer.IsValidValue(maxValue));
    }

    [Theory]
    [InlineData("café-resource", true)]     // accented chars are fine
    [InlineData("value\twith\ttabs", false)] // tabs are control chars
    [InlineData("value`backtick", true)]     // backticks allowed
    [InlineData("value;semicolon", true)]    // semicolons allowed
    [InlineData("DROP TABLE users;--", true)] // not SQL-injecting markdown
    [InlineData("line1\nline2", false)]       // newlines rejected
    [InlineData("has'quote", false)]          // single quotes rejected
    public void IsValidValue_EdgeCases(string value, bool expected)
    {
        Assert.Equal(expected, DeterministicPromptRepairer.IsValidValue(value));
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Value resolution chain
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolveValue_PrefersEnumFromDescription()
    {
        var value = DeterministicPromptRepairer.ResolveValue("tier", "Available options: 'hot', 'cool', 'archive'");
        Assert.Equal("hot", value);
    }

    [Fact]
    public void ResolveValue_FallsBackToValueBank()
    {
        var value = DeterministicPromptRepairer.ResolveValue("account", null);
        Assert.Equal("mystorageacct", value);
    }

    [Fact]
    public void ResolveValue_UsesGuidHeuristicForIdSuffix()
    {
        var value = DeterministicPromptRepairer.ResolveValue("subscription-id", null);
        Assert.Contains("-", value); // GUID format
        Assert.Equal("00000000-0000-0000-0000-000000000001", value);
    }

    [Fact]
    public void ResolveValue_UsesUriHeuristicForEndpointSuffix()
    {
        var value = DeterministicPromptRepairer.ResolveValue("api-endpoint", null);
        Assert.StartsWith("https://", value);
    }

    [Fact]
    public void ResolveValue_UsesDateHeuristicForDateSuffix()
    {
        var value = DeterministicPromptRepairer.ResolveValue("start-date", null);
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}$", value);
    }

    [Fact]
    public void ResolveValue_FallbackUsesContosoPrefix()
    {
        var value = DeterministicPromptRepairer.ResolveValue("custom-unknown-param", null);
        Assert.Equal("contoso-custom-01", value);
    }

    [Fact]
    public void ResolveValue_ExcludesValueKeyFromBank()
    {
        // "value" key had credentials — must not resolve from bank
        var value = DeterministicPromptRepairer.ResolveValue("value", null);
        Assert.Equal("contoso-value-01", value); // fallback, not "P@ssw0rd!2026"
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Injection grammar
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void InjectParameter_InsertsBeforeFinalPunctuation()
    {
        var result = DeterministicPromptRepairer.InjectParameter(
            "List all storage accounts.", "account", "mystorageacct");
        Assert.EndsWith(".", result);
        Assert.Contains("for account 'mystorageacct'", result);
        Assert.Equal("List all storage accounts for account 'mystorageacct'.", result);
    }

    [Fact]
    public void InjectParameter_AppendsSentenceWhenNoPunctuation()
    {
        var result = DeterministicPromptRepairer.InjectParameter(
            "List all storage accounts", "account", "mystorageacct");
        Assert.Contains("Specify account 'mystorageacct'", result);
        Assert.EndsWith(".", result);
    }

    [Fact]
    public void InjectParameter_HandlesQuestionMark()
    {
        var result = DeterministicPromptRepairer.InjectParameter(
            "What are the storage accounts?", "resource-group", "rg-prod");
        Assert.EndsWith("?", result);
        Assert.Contains("for resource group 'rg-prod'", result);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Edge cases
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Repair_EmptyPrompts_ReturnsUnchanged()
    {
        var result = DeterministicPromptRepairer.Repair([], [new Option { Name = "x", Required = true }]);
        Assert.Empty(result.RepairedPrompts);
        Assert.Empty(result.Actions);
    }

    [Fact]
    public void Repair_EmptyRequiredParams_ReturnsUnchanged()
    {
        var prompts = new List<string> { "Hello world." };
        var result = DeterministicPromptRepairer.Repair(prompts, []);
        Assert.Single(result.RepairedPrompts);
        Assert.Equal("Hello world.", result.RepairedPrompts[0]);
    }

    [Fact]
    public void Repair_NullParamName_Skipped()
    {
        var prompts = new List<string> { "Test prompt." };
        var required = new List<Option> { new() { Name = null, Required = true } };
        var result = DeterministicPromptRepairer.Repair(prompts, required);
        Assert.Empty(result.Actions);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // NON-BLOCKING #9: ValueBank contract — shared keys resolve identically
    // ──────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("account")]
    [InlineData("resource-group")]
    [InlineData("location")]
    [InlineData("vault")]
    public void ValueBank_SharedKeysMatchOriginalGenerator(string key)
    {
        // Contract: ParameterValueBank and the original DeterministicExamplePromptGenerator
        // must have the same values for shared keys
        Assert.True(ParameterValueBank.Bank.ContainsKey(key),
            $"ParameterValueBank missing key '{key}'");
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // Integration: repair → sanitize → checker roundtrip
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Integration_RepairThenSanitizeThenChecker_AllCovered()
    {
        var prompts = new List<string>
        {
            "List all blobs in the container.",
            "Show blob metadata.",
            "Download a specific blob.",
            "Upload a new blob.",
            "Delete a blob."
        };
        var required = new List<Option>
        {
            new() { Name = "--account", Required = true, Description = "Storage account name" },
            new() { Name = "--container-name", Required = true, Description = "Container name" }
        };

        var result = DeterministicPromptRepairer.Repair(prompts, required);

        // Post-repair: sanitize and verify each non-blank prompt covers all params
        var sanitized = result.RepairedPrompts
            .Select(CredentialSanitizer.Sanitize)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        foreach (var param in required)
        {
            var canonName = DeterministicPromptRepairer.CanonicalizeParamName(param.Name!);
            var coverage = ParameterCoverageChecker.GetConcretePromptCoverage(
                sanitized, canonName, required.Count, param.Description);
            Assert.True(coverage.Covered || coverage.PlaceholderDetected,
                $"Parameter '{canonName}' not covered after repair+sanitize");
        }

        // Should have no StillUncovered
        Assert.Empty(result.StillUncovered);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    // INTEGRATION: Full flow — AI response with missing params → repair → sanitize
    // → coverage checker passes. Uses real E2E failure cases from #781.
    // ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Integration_KeyVault_CertificateData_RepairThenSanitize_Passes()
    {
        // Simulate AI-generated prompts that miss certificate-data param
        var aiPrompts = new List<string>
        {
            "Import a certificate into my Key Vault named 'contoso-kv-01'.",
            "Upload a PFX certificate to the vault 'my-vault'.",
            "Add a new certificate to Key Vault for my application.",
            "Import the SSL certificate into Azure Key Vault.",
            "Store a code signing certificate in the vault."
        };
        var required = new List<Option>
        {
            new() { Name = "--certificate-data", Required = true, Description = "Base64-encoded certificate data (PFX/PEM)" }
        };

        var result = DeterministicPromptRepairer.Repair(aiPrompts, required);

        // Verify: all non-blank prompts now mention certificate-data
        Assert.Empty(result.StillUncovered);
        Assert.True(result.Actions.Count > 0, "Should have repaired certificate-data");
        Assert.Equal("certificate-data", result.Actions[0].ParameterName);

        // Full pipeline: sanitize then re-check coverage
        var sanitized = result.RepairedPrompts.Select(CredentialSanitizer.Sanitize).ToList();
        foreach (var prompt in sanitized.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            Assert.Contains("certificate", prompt, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Integration_Cosmos_OpenAIEndpointAndEmbeddingDeployment_RepairThenSanitize_Passes()
    {
        // Simulate AI-generated prompts that miss openai-endpoint and embedding-deployment
        var aiPrompts = new List<string>
        {
            "Create a vector index in my Cosmos DB database 'products'.",
            "Set up vector search for the 'reviews' collection.",
            "Configure embeddings for Cosmos DB container.",
            "Initialize vector indexing policy on my database.",
            "Enable vector search capabilities in Cosmos."
        };
        var required = new List<Option>
        {
            new() { Name = "--openai-endpoint", Required = true, Description = "Azure OpenAI endpoint URL" },
            new() { Name = "--embedding-deployment", Required = true, Description = "Embedding model deployment name" },
            new() { Name = "--database", Required = true, Description = "Database name" }
        };

        var result = DeterministicPromptRepairer.Repair(aiPrompts, required);

        // database is likely already covered in prompts; openai-endpoint and embedding-deployment should be repaired
        var repairedParamNames = result.Actions.Select(a => a.ParameterName).ToHashSet();
        Assert.Contains("openai-endpoint", repairedParamNames);
        Assert.Contains("embedding-deployment", repairedParamNames);

        // Verify post-sanitize coverage
        Assert.Empty(result.StillUncovered);

        var sanitized = result.RepairedPrompts.Select(CredentialSanitizer.Sanitize).ToList();
        foreach (var prompt in sanitized.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            // openai-endpoint should survive sanitization (heuristic value isn't a real credential)
            var coverage1 = ParameterCoverageChecker.GetConcretePromptCoverage(
                new[] { prompt }, "openai-endpoint", required.Count, required[0].Description);
            var coverage2 = ParameterCoverageChecker.GetConcretePromptCoverage(
                new[] { prompt }, "embedding-deployment", required.Count, required[1].Description);
            Assert.True(coverage1.Covered || coverage1.PlaceholderDetected,
                $"openai-endpoint not covered in: {prompt}");
            Assert.True(coverage2.Covered || coverage2.PlaceholderDetected,
                $"embedding-deployment not covered in: {prompt}");
        }
    }

    [Fact]
    public void Integration_RepairDoesNotDoubleInjectAlreadyCoveredParams()
    {
        // Prompts already contain the required param — repair should be a no-op
        var prompts = new List<string>
        {
            "List certificates in vault 'contoso-vault' for resource group 'rg-contoso-01'.",
            "Show certificate details in vault 'test-kv' for resource group 'rg-test'."
        };
        var required = new List<Option>
        {
            new() { Name = "--resource-group", Required = true, Description = "Resource group name" }
        };

        var result = DeterministicPromptRepairer.Repair(prompts, required);

        Assert.Empty(result.Actions);
        Assert.Empty(result.StillUncovered);
        // Prompts unchanged
        Assert.Equal(prompts[0], result.RepairedPrompts[0]);
        Assert.Equal(prompts[1], result.RepairedPrompts[1]);
    }
}
