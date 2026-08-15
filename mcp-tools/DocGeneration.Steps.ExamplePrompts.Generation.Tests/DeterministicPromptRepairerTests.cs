// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ExamplePromptGeneratorStandalone.Generators;
using ExamplePromptGeneratorStandalone.Sanitizers;
using Shared;
using Xunit;

namespace DocGeneration.Steps.ExamplePrompts.Generation.Tests;

/// <summary>
/// Tests for DeterministicPromptRepairer after the canonical-manifest migration.
/// All coverage decisions must flow through CanonicalCoverageEvaluator.
/// </summary>
public class DeterministicPromptRepairerTests
{
    [Fact]
    public void Repair_InjectsAllMissingParamsIntoEveryNonBlankPrompt()
    {
        var prompts = new List<string>
        {
            "List all blobs in the specified container.",
            "Show me the metadata for a given blob.",
            "Download files from blob storage.",
            "",
            "Check blob storage metrics."
        };
        var manifest = BuildManifest(
            "azmcp storage blob list",
            "storage",
            BuildEntry("account", "Account name", description: "Storage account name"),
            BuildEntry("resource-group", "Resource group", description: "Resource group name"));

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);
        var nonBlank = result.RepairedPrompts.Where(prompt => !string.IsNullOrWhiteSpace(prompt)).ToList();

        Assert.True(nonBlank.Count >= 4, "Should have at least 4 non-blank prompts");
        foreach (var prompt in nonBlank)
        {
            var coverage = CanonicalCoverageEvaluator.EvaluateParameterCoverage([prompt], manifest);
            Assert.True(coverage.AllRequiredCovered, $"Prompt lacks canonical coverage after repair: {prompt}");
        }
    }

    [Fact]
    public void Repair_BlankPromptsAreNotModified()
    {
        var prompts = new List<string> { "List accounts.", "", "   " };
        var manifest = BuildManifest(
            "azmcp storage account list",
            "storage",
            BuildEntry("account", "Account name", description: "Account name"));

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        Assert.Equal("", result.RepairedPrompts[1]);
        Assert.Equal("   ", result.RepairedPrompts[2]);
    }

    [Fact]
    public void Repair_StillUncovered_DetectedAfterSanitization()
    {
        var prompts = new List<string> { "Deploy the resources to a specific region." };
        var manifest = BuildManifest(
            "azmcp deployment create",
            "resources",
            BuildEntry("location", "Location", description: "Azure region for deployment"));

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        Assert.DoesNotContain("location", result.StillUncovered);
        Assert.True(CanonicalCoverageEvaluator.EvaluateParameterCoverage(
            result.RepairedPrompts.Select(CredentialSanitizer.Sanitize).ToList(),
            manifest).AllRequiredCovered);
    }

    [Fact]
    public void Repair_ValueBankExcludesValueKey_CredentialSafe()
    {
        Assert.False(ParameterValueBank.Bank.ContainsKey("value"),
            "ParameterValueBank must not contain 'value' key (credential risk)");
    }

    [Fact]
    public void Repair_InjectedEnumValueSurvivesSanitizer_NotInStillUncovered()
    {
        var prompts = new List<string> { "Create a new storage tier." };
        var manifest = BuildManifest(
            "azmcp storage account update",
            "storage",
            BuildEntry("tier", "Tier", description: "Available options: 'standard', 'premium'"));

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);
        var sanitized = result.RepairedPrompts.Select(CredentialSanitizer.Sanitize).ToList();

        Assert.DoesNotContain("tier", result.StillUncovered);
        Assert.True(CanonicalCoverageEvaluator.EvaluateParameterCoverage(sanitized, manifest).AllRequiredCovered);
    }

    [Fact]
    public void Repair_InjectedFallbackValue_IsSafeAndDoesNotEchoRejectedEnum()
    {
        var prompts = new List<string> { "Authenticate the user." };
        var manifest = BuildManifest(
            "azmcp auth validate",
            "auth",
            BuildEntry("token-input", "Token input", description: "Available options: '******'"));

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        Assert.Single(result.Actions);
        Assert.Equal("token-input", result.Actions[0].ParameterName);
        Assert.True(DeterministicPromptRepairer.IsValidValue(result.Actions[0].InjectedValue));
        Assert.DoesNotContain("******", result.RepairedPrompts[0]);
    }

    [Fact]
    public void Repair_SkipsAlreadyCoveredByAuthorizedPlaceholder()
    {
        var prompts = new List<string> { "List items for <account> in resource group 'rg-prod'" };
        var manifest = BuildManifest(
            "azmcp storage account show",
            "storage",
            BuildEntry("account", "Account name", description: "Account name"),
            BuildEntry("resource-group", "Resource group", description: "RG name"));

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        Assert.Empty(result.Actions);
        Assert.Empty(result.StillUncovered);
    }

    [Fact]
    public void BuildRetryFeedback_IncludesCanonicalParamNamesPromptIndicesAndRewriteExample()
    {
        var prompts = new List<string>
        {
            "Scale the deployment.",
            "Increase the instance count.",
            "",
            "Show me the current capacity."
        };
        var manifest = BuildManifest(
            "azmcp compute vmss update",
            "compute",
            BuildEntry("resource-group", "Resource group name", description: "Resource group name"));

        var feedback = DeterministicPromptRepairer.BuildRetryFeedback(prompts, manifest);

        Assert.Contains("resource-group", feedback, StringComparison.Ordinal);
        Assert.Contains("Prompt #1", feedback, StringComparison.Ordinal);
        Assert.Contains("Prompt #2", feedback, StringComparison.Ordinal);
        Assert.Contains("Prompt #4", feedback, StringComparison.Ordinal);
        Assert.Contains("Rewrite example", feedback, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildRetryFeedback_ReturnsEmptyWhenAllRequiredCovered()
    {
        var prompts = new List<string> { "Scale the deployment for resource group 'rg-prod'." };
        var manifest = BuildManifest(
            "azmcp compute vmss update",
            "compute",
            BuildEntry("resource-group", "Resource group name", description: "Resource group name"));

        var feedback = DeterministicPromptRepairer.BuildRetryFeedback(prompts, manifest);

        Assert.Equal(string.Empty, feedback);
    }

    [Fact]
    public void CanonicalizeParamName_StripsLeadingDashes()
    {
        Assert.Equal("account", DeterministicPromptRepairer.CanonicalizeParamName("--account"));
        Assert.Equal("resource-group", DeterministicPromptRepairer.CanonicalizeParamName("--resource-group"));
        Assert.Equal("name", DeterministicPromptRepairer.CanonicalizeParamName("name"));
    }

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
    [InlineData("café-resource", true)]
    [InlineData("value\twith\ttabs", false)]
    [InlineData("value`backtick", true)]
    [InlineData("value;semicolon", true)]
    [InlineData("DROP TABLE users;--", true)]
    [InlineData("line1\nline2", false)]
    [InlineData("has'quote", false)]
    public void IsValidValue_EdgeCases(string value, bool expected)
    {
        Assert.Equal(expected, DeterministicPromptRepairer.IsValidValue(value));
    }

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
        Assert.Contains("-", value);
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
        var value = DeterministicPromptRepairer.ResolveValue("value", null);
        Assert.Equal("contoso-value-01", value);
    }

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

    [Fact]
    public void Repair_EmptyPrompts_ReturnsUnchanged()
    {
        var manifest = BuildManifest("azmcp test tool", "test", BuildEntry("x", "X"));
        var result = DeterministicPromptRepairer.Repair([], manifest);
        Assert.Empty(result.RepairedPrompts);
        Assert.Empty(result.Actions);
    }

    [Fact]
    public void Repair_EmptyManifest_ReturnsUnchanged()
    {
        var prompts = new List<string> { "Hello world." };
        var manifest = BuildManifest("azmcp test tool", "test");

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        Assert.Single(result.RepairedPrompts);
        Assert.Equal("Hello world.", result.RepairedPrompts[0]);
    }

    [Theory]
    [InlineData("account")]
    [InlineData("resource-group")]
    [InlineData("location")]
    [InlineData("vault")]
    public void ValueBank_SharedKeysMatchOriginalGenerator(string key)
    {
        Assert.True(ParameterValueBank.Bank.ContainsKey(key),
            $"ParameterValueBank missing key '{key}'");
    }

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
        var manifest = BuildManifest(
            "azmcp storage blob list",
            "storage",
            BuildEntry("account", "Account name", description: "Storage account name"),
            BuildEntry("container-name", "Container name", description: "Container name"));

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);
        var sanitized = result.RepairedPrompts
            .Select(CredentialSanitizer.Sanitize)
            .Where(prompt => !string.IsNullOrWhiteSpace(prompt))
            .ToList();

        Assert.True(CanonicalCoverageEvaluator.EvaluateParameterCoverage(sanitized, manifest).AllRequiredCovered);
        Assert.Empty(result.StillUncovered);
    }

    [Fact]
    public void Integration_KeyVault_CertificateData_RepairThenSanitize_Passes()
    {
        var aiPrompts = new List<string>
        {
            "Import a certificate into my Key Vault named 'contoso-kv-01'.",
            "Upload a PFX certificate to the vault 'my-vault'.",
            "Add a new certificate to Key Vault for my application.",
            "Import the SSL certificate into Azure Key Vault.",
            "Store a code signing certificate in the vault."
        };
        var manifest = BuildManifest(
            "azmcp keyvault certificate import",
            "keyvault",
            BuildEntry("certificate-data", "Certificate data", description: "Base64-encoded certificate data (PFX/PEM)"));

        var result = DeterministicPromptRepairer.Repair(aiPrompts, manifest);
        var sanitized = result.RepairedPrompts.Select(CredentialSanitizer.Sanitize).ToList();

        Assert.Empty(result.StillUncovered);
        Assert.Contains(result.Actions, action => action.ParameterName == "certificate-data");
        Assert.True(CanonicalCoverageEvaluator.EvaluateParameterCoverage(sanitized, manifest).AllRequiredCovered);
    }

    [Fact]
    public void Integration_Cosmos_OpenAIEndpointAndEmbeddingDeployment_RepairThenSanitize_Passes()
    {
        var aiPrompts = new List<string>
        {
            "Create a vector index in my Cosmos DB database 'products'.",
            "Set up vector search for the 'reviews' collection.",
            "Configure embeddings for Cosmos DB container.",
            "Initialize vector indexing policy on my database.",
            "Enable vector search capabilities in Cosmos."
        };
        var manifest = BuildManifest(
            "azmcp cosmos vector create",
            "cosmos",
            BuildEntry("openai-endpoint", "OpenAI endpoint", description: "Azure OpenAI endpoint URL"),
            BuildEntry("embedding-deployment", "Embedding deployment", description: "Embedding model deployment name"),
            BuildEntry("database", "Database name", description: "Database name"));

        var result = DeterministicPromptRepairer.Repair(aiPrompts, manifest);
        var sanitized = result.RepairedPrompts.Select(CredentialSanitizer.Sanitize).ToList();
        var repairedParamNames = result.Actions.Select(action => action.ParameterName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("openai-endpoint", repairedParamNames);
        Assert.Contains("embedding-deployment", repairedParamNames);
        Assert.Empty(result.StillUncovered);
        Assert.True(CanonicalCoverageEvaluator.EvaluateParameterCoverage(sanitized, manifest).AllRequiredCovered);
    }

    [Fact]
    public void Integration_RepairDoesNotDoubleInjectAlreadyCoveredParams()
    {
        var prompts = new List<string>
        {
            "List certificates in vault 'contoso-vault' for resource group 'rg-contoso-01'.",
            "Show certificate details in vault 'test-kv' for resource group 'rg-test'."
        };
        var manifest = BuildManifest(
            "azmcp keyvault certificate list",
            "keyvault",
            BuildEntry("resource-group", "Resource group", description: "Resource group name"));

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        Assert.Empty(result.Actions);
        Assert.Empty(result.StillUncovered);
        Assert.Equal(prompts[0], result.RepairedPrompts[0]);
        Assert.Equal(prompts[1], result.RepairedPrompts[1]);
    }

    [Fact]
    public void Repair_ExposesCanonicalCoverageBeforeAndAfterRepair()
    {
        var prompts = new List<string> { "Get key-values from App Configuration store <app_config_store_name>" };
        var manifest = BuildManifest(
            "azmcp appconfig kv get",
            "appconfig",
            BuildEntry("account", "Account name", description: "Account name"));

        var result = DeterministicPromptRepairer.Repair(prompts, manifest);

        Assert.Single(result.InitialCoverage);
        Assert.Equal(CoverageVerdict.Missing, result.InitialCoverage[0].Verdict);
        Assert.Single(result.FinalCoverage);
        Assert.True(result.FinalCoverage[0].Verdict is CoverageVerdict.Concrete or CoverageVerdict.AuthorizedPlaceholder);
    }

    private static CanonicalParameterManifest BuildManifest(
        string command,
        string ns,
        params CanonicalParameterEntry[] parameters)
    {
        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var parameter in parameters)
        {
            foreach (var alias in parameter.PlaceholderAliases)
            {
                index.TryAdd(alias, parameter.CanonicalName);
            }
        }

        return new CanonicalParameterManifest(
            "2.0",
            command,
            ns,
            new ManifestSourceIdentity("1.0.0", "2026-01-01T00:00:00Z"),
            parameters,
            index);
    }

    private static CanonicalParameterEntry BuildEntry(
        string canonical,
        string displayName,
        bool required = true,
        string? description = null,
        string[]? displayAliases = null,
        string[]? placeholderAliases = null)
    {
        var resolvedDisplayAliases = displayAliases ??
        [
            CanonicalParameterNormalizer.Normalize(displayName),
            canonical
        ];
        var resolvedPlaceholderAliases = placeholderAliases ??
        [
            canonical,
            canonical.Replace('-', '_')
        ];

        return new CanonicalParameterEntry(
            canonical,
            displayName,
            resolvedDisplayAliases.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            resolvedPlaceholderAliases.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            required,
            required ? "Required" : "Optional",
            false,
            description ?? $"The {displayName.ToLowerInvariant()}.");
    }
}
