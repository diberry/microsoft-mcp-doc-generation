using Shared;
using Xunit;

namespace Shared.Tests;

public class ParameterCoverageCheckerTests
{
    // ── ConvertToSlug ───────────────────────────────────────────────

    [Theory]
    [InlineData("account", "account")]
    [InlineData("resource-group", "resource-group")]
    [InlineData("ResourceGroup", "resource-group")]
    [InlineData("outputAudio", "output-audio")]
    [InlineData("", "")]
    [InlineData("  ", "")]
    public void ConvertToSlug_ReturnsExpected(string input, string expected)
    {
        var result = ParameterCoverageChecker.ConvertToSlug(input);
        Assert.Equal(expected, result);
    }

    // ── RemoveMarkup ────────────────────────────────────────────────

    [Theory]
    [InlineData("**bold**", "bold")]
    [InlineData("`code`", "code")]
    [InlineData("<em>html</em>", "html")]
    [InlineData("  extra   spaces  ", "extra spaces")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void RemoveMarkup_ReturnsExpected(string? input, string expected)
    {
        var result = ParameterCoverageChecker.RemoveMarkup(input!);
        Assert.Equal(expected, result);
    }

    // ── GetConcretePromptCoverage ───────────────────────────────────

    [Fact]
    public void ExactSlugMatch_WithConcreteValue_ReturnsCovered()
    {
        var prompts = new[] { "List resources for account named 'myaccount123'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "account", 1);

        Assert.True(result.Covered);
    }

    [Fact]
    public void MultiWordParameter_MatchesVariants()
    {
        // "resource-group" should match "resource group" and "resource_group"
        var prompts = new[] { "Deploy to resource group named 'my-rg'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "resource-group", 2);

        Assert.True(result.Covered);
    }

    [Fact]
    public void TypeSuffixStripping_SubscriptionName_MatchesBySubscription()
    {
        // "subscription-name" should strip "-name" suffix and match "subscription"
        var prompts = new[] { "Use subscription named 'my-sub-001'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "subscription-name", 1);

        Assert.True(result.Covered);
    }

    [Theory]
    [InlineData("<account>")]
    [InlineData("{account}")]
    [InlineData("[account]")]
    [InlineData("`account`")]
    public void PlaceholderDetection_VariousFormats(string placeholder)
    {
        var prompts = new[] { $"List resources for {placeholder}" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "account", 1);

        Assert.False(result.Covered, "Placeholder should not count as concrete coverage");
        Assert.True(result.PlaceholderDetected, $"Should detect placeholder in: {placeholder}");
    }

    [Fact]
    public void DoubleWrappedPlaceholder_DetectsPlaceholder()
    {
        var prompts = new[] { "List resources for `<account>`" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "account", 1);

        Assert.False(result.Covered);
        Assert.True(result.PlaceholderDetected);
    }

    [Fact]
    public void SemanticFallback_KeyNameMatchesKey()
    {
        // <key_name> should match parameter "key" via semantic word-level fallback
        var prompts = new[] { "Get secret with <key_name>" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "key", 1);

        Assert.False(result.Covered);
        Assert.True(result.PlaceholderDetected);
    }

    [Fact]
    public void SemanticFallback_PluralIdParameterMatchesSingularIdPlaceholder()
    {
        var prompts = new[] { "Delete workbook with resource ID <workbook_resource_id>." };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "workbook-ids", 1);

        Assert.False(result.Covered);
        Assert.True(result.PlaceholderDetected);
    }

    [Fact]
    public void PluralIdParameter_WithConcreteValue_ReturnsCovered()
    {
        // Complementary positive case (reviewer: Statler — PR #725):
        // workbook-ids with real quoted values must be Covered=true, PlaceholderDetected=false.
        // Proves the plural-ID/singular-placeholder fix did not over-suppress real coverage.
        var prompts = new[] { "Get workbook 'my-workbook-001' from the workspace." };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "workbook-ids", 1);

        Assert.True(result.Covered);
        Assert.False(result.PlaceholderDetected);
    }

    [Fact]
    public void NoMatch_ReturnsCoveredFalse()
    {
        var prompts = new[] { "List all virtual machines in the region" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "account", 2);

        Assert.False(result.Covered);
        Assert.False(result.PlaceholderDetected);
    }

    [Fact]
    public void EmptyPromptList_ReturnsCoveredFalse()
    {
        var prompts = Array.Empty<string>();
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "account", 1);

        Assert.False(result.Covered);
        Assert.False(result.PlaceholderDetected);
    }

    // ── Defect regression tests ─────────────────────────────────────

    [Fact]
    public void SingleWordParameter_SubstringInLongerWord_WithConcreteValue_ReturnsCovered()
    {
        // Defect 1: "Name" should not match inside "named" incorrectly
        var prompts = new[] { "Delete the file share named 'myshare'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Name", 1);
        Assert.True(result.Covered);
    }

    [Fact]
    public void ArrayParameter_JsonObjectArrayValue_ReturnsCovered()
    {
        // Defect 2: JSON objects in arrays should be valid concrete values
        var prompts = new[] { "Create chat completion with messages [{'role': 'user', 'content': 'hello'}]" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Message array", 2);
        Assert.True(result.Covered);
    }

    [Fact]
    public void MultiWordParameter_AtSentenceEnd_ReturnsCovered()
    {
        // Defect 3: Multi-word structural parameters at sentence end
        var prompts = new[] { "Generate an architecture diagram from the raw mcp tool input" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Raw mcp tool input", 2);
        Assert.True(result.Covered);
    }

    [Fact]
    public void AbbreviationInParentheses_FoundInPrompt_ReturnsCovered()
    {
        // Defect 4: Parenthetical abbreviation should match
        var prompts = new[] { "Create a new virtual machine scale set named 'my-vmss' in resource group 'rg-prod'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Virtual machine scale set (VMSS) name", 2);
        Assert.True(result.Covered);
    }

    [Fact]
    public void VaguePrompt_NoConcreteValue_ReturnsFalse()
    {
        // Negative test: genuinely missing concrete value should still fail
        var prompts = new[] { "Get the app settings" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "App", 2);
        Assert.False(result.Covered);
    }

    // ── CLI switch prefix stripping (behavioral) ────────────────────

    [Fact]
    public void ParameterWithoutPrefix_Name_WithConcreteValue_ReturnsCovered()
    {
        // Parameter "name" (no --prefix) with prompt containing a concrete value
        var prompts = new[] { "Delete file share named 'myshare'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "name", 1);
        Assert.True(result.Covered);
    }

    [Fact]
    public void ParameterWithoutPrefix_MessageArray_WithJsonArray_ReturnsCovered()
    {
        // Parameter "message-array" (no --prefix) with JSON array value
        var prompts = new[] { "Send messages [{'role':'user','content':'hello'}]" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "message-array", 2);
        Assert.True(result.Covered);
    }

    [Fact]
    public void ParameterWithoutPrefix_Param_WithConcreteValue_ReturnsCovered()
    {
        // Parameter "param" (no --prefix) with concrete value
        var prompts = new[] { "Get the server parameter named 'max_connections'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "param", 1);
        Assert.True(result.Covered);
    }

    // ── JSON array placeholder rejection ────────────────────────────

    [Fact]
    public void JsonArrayPlaceholder_ReturnsCoveredFalse()
    {
        // Placeholder-like content in brackets should not count as concrete
        var prompts = new[] { "Process items [{config}]" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "items", 2);
        Assert.False(result.Covered, "Placeholder-like JSON array should not be covered");
    }

    [Fact]
    public void RealJsonArray_ReturnsCoveredTrue()
    {
        // Real JSON object array with concrete values should count as covered
        var prompts = new[] { "Process items [{'id': 1, 'name': 'test'}]" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "items", 2);
        Assert.True(result.Covered, "Real JSON array should be covered");
    }

    // ── Issue #161: Array parameter patterns ─────────────────────────
    // AI-generated prompts for array params don't always include literal
    // JSON array syntax. The checker must handle natural language references.

    [Fact]
    public void ArrayParam_NaturalLanguageReference_WithConcreteValue()
    {
        // AI prompt: describes the messages but uses natural language, not JSON array
        var prompts = new[] { "Create a chat completion with the user message 'What is the weather today?'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "message-array", 4);
        Assert.True(result.Covered,
            "Array param 'message-array' should match when base word 'message' appears with a concrete quoted value");
    }

    [Fact]
    public void ArrayParam_PluralBaseWord_WithConcreteValue()
    {
        // AI uses plural form "messages" instead of exact param name
        var prompts = new[] { "Send messages 'Hello, how can I help?' to the chat completion endpoint" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "message-array", 4);
        Assert.True(result.Covered,
            "Array param should match when plural base word 'messages' appears with concrete value");
    }

    [Fact]
    public void ArrayParam_JsonArraySyntax_WithObjects()
    {
        // Best case: AI actually includes JSON array syntax
        var prompts = new[] { "Create a chat completion with message array [{'role': 'user', 'content': 'hello'}]" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "message-array", 4);
        Assert.True(result.Covered,
            "Array param with literal JSON array syntax should definitely be covered");
    }

    [Fact]
    public void ArrayParam_NoMention_ReturnsFalse()
    {
        // Negative: prompt doesn't mention the array param at all
        var prompts = new[] { "Create a chat completion using deployment 'gpt-4' in resource group 'rg-prod'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "message-array", 4);
        Assert.False(result.Covered,
            "Array param should NOT be covered when neither 'message' nor 'array' appears");
    }

    // ── Issue #161: Single-word parameter patterns ───────────────────
    // Params like "name", "key", "app" are common English words that appear
    // in many contexts. The checker must handle them robustly.

    [Fact]
    public void SingleWordParam_Name_InUpdateContext()
    {
        // AI prompt describes updating a named resource — "name" is implicit
        var prompts = new[] { "Update the file share 'analytics-share' to increase quota to 200 GB" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "name", 2);
        Assert.True(result.Covered,
            "Single-word param 'name' should be covered when a concrete quoted resource name is the first/primary arg");
    }

    [Fact]
    public void SingleWordParam_Name_WithExplicitNamed()
    {
        // AI prompt uses "named" keyword with concrete value
        var prompts = new[] { "Update the file share named 'data-share' in resource group 'rg-storage'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "name", 2);
        Assert.True(result.Covered,
            "Single-word param 'name' should match 'named' + concrete value");
    }

    [Fact]
    public void SingleWordParam_Name_WithCalledKeyword()
    {
        // AI prompt uses "called" instead of "named"
        var prompts = new[] { "Delete the file share called 'temp-share'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "name", 1);
        Assert.True(result.Covered,
            "Single-word param 'name' should match when any concrete quoted resource identifier exists");
    }

    [Fact]
    public void ArrayParam_JsonInsideQuotes_ReturnsCovered()
    {
        // The EXACT pattern foundryextensions generates: JSON array inside single quotes
        var prompts = new[] { "Create a chat completion with message-array '[{\"role\":\"user\",\"content\":\"Hello\"}]' for resource-group 'rg-foundry'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "message-array", 4);
        Assert.True(result.Covered,
            "Quoted value containing JSON (braces/brackets inside quotes) should be accepted as concrete");
    }

    [Fact]
    public void SingleWordParam_Name_NoConcreteValue_ReturnsFalse()
    {
        // Negative: prompt uses "name" generically without concrete value
        var prompts = new[] { "Update the file share quota" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "name", 2);
        Assert.False(result.Covered,
            "Single-word param 'name' should NOT be covered without any concrete value");
    }

    [Fact]
    public void SingleWordParam_Key_WithConcreteValue()
    {
        // Another single-word param: "key"
        var prompts = new[] { "Get the secret key named 'api-key-prod' from the vault" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "key", 2);
        Assert.True(result.Covered,
            "Single-word param 'key' should match with concrete value");
    }

    [Fact]
    public void SingleWordParam_Param_WithConcreteValue()
    {
        // "param" — the postgres server-param-get case
        var prompts = new[] { "Get the server parameter 'max_connections' from the PostgreSQL server 'prod-pg'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "param", 2);
        Assert.True(result.Covered,
            "Single-word param 'param' should match 'parameter' + concrete value");
    }

    [Fact]
    public void SingleWordParam_Param_PluralForm()
    {
        // AI uses "parameters" (plural) instead of "param"
        var prompts = new[] { "List all server parameters for PostgreSQL server 'prod-pg'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "param", 2);
        Assert.True(result.Covered,
            "Single-word param 'param' should match its expanded form 'parameters'");
    }

    // ── Issue #161: Combined edge cases ──────────────────────────────

    [Fact]
    public void SingleWordParam_AsOnlyRequiredParam_WithQuotedValue()
    {
        // When a tool has only 1 required param and a quoted value exists anywhere
        var prompts = new[] { "Delete 'my-resource'" };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "name", 1);
        Assert.True(result.Covered,
            "Single required param 'name' with ANY quoted value in a 1-param tool should be covered");
    }

    [Fact]
    public void CamelCaseParam_OutputAudio_MatchesNaturalLanguage()
    {
        // "outputAudio" should match "output audio 'welcome.wav'" — camelCase splits into words
        var prompts = new[] { "Synthesize speech from text 'Hello' using endpoint 'https://my-service.cognitiveservices.azure.com/' and save to output audio 'welcome.wav'." };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "outputAudio", 3);

        Assert.True(result.Covered,
            "camelCase param 'outputAudio' should match 'output audio' + concrete quoted value in prompt");
    }

    // ── Issue #665: --account param vs domain synonym "store" ────────
    // The appconfig system prompt was allowing "store 'X'" as an alias for --account.
    // The checker validates prompts contain the CLI parameter name (or a close variant),
    // NOT just service-domain synonyms. If the LLM writes "store 'my-appconfig'" instead
    // of "account 'my-appconfig'", validation must fail so the prompt is fixed.

    [Fact]
    public void AccountParam_AppConfigStorePrompt_ReturnsCovered()
    {
        // Prompt that correctly uses the CLI parameter word "account" — must be covered.
        var prompts = new[] { "Delete the key 'my-key' from account 'my-appconfig'." };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "account", 1);
        Assert.True(result.Covered, "Prompt with 'account' word should be covered");
    }

    [Fact]
    public void AccountParam_DomainTermOnly_ReturnsFalse()
    {
        // Prompt using the domain synonym "store" but NOT the word "account".
        // Domain synonyms are not valid substitutes for the CLI parameter name.
        // This guards against system prompts that encourage the LLM to use service
        // domain terms (e.g., "store", "vault", "workspace") instead of the actual
        // CLI flag name, which causes coverage validation to block the namespace.
        var prompts = new[] { "Delete the key 'my-key' in App Configuration store 'my-appconfig'." };
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "account", 1);
        Assert.False(result.Covered, "Prompt with only domain term 'store' but no 'account' word should not be covered");
    }

    // ── Enum-aware coverage (closed "Available options" value sets) ──
    // Some required parameters take a value from a closed enum whose display name (e.g.
    // "Resource name") differs from the option values (e.g. 'storage_storageaccounts').
    // Authoritative prompts reference a concrete option ("Storage Account") rather than
    // the parameter name, which the name-based checker cannot see. Enum matching resolves
    // this false positive additively and must not weaken non-enum behavior.

    // ── ParseAllowedValues ──────────────────────────────────────────

    [Fact]
    public void ParseAllowedValues_AvailableOptions_ExtractsQuotedValues()
    {
        const string description =
            "The Azure resource type for which to get rules. Available options: 'keyvault_vaults', "
            + "'storage_storageaccounts', 'web_serverfarms'.";
        var values = ParameterCoverageChecker.ParseAllowedValues(description);

        Assert.Equal(new[] { "keyvault_vaults", "storage_storageaccounts", "web_serverfarms" }, values);
    }

    [Fact]
    public void ParseAllowedValues_AllowedValuesTrigger_ExtractsQuotedValues()
    {
        // Different Azure service (Cosmos DB) + different trigger phrase.
        const string description =
            "The consistency level to apply. Allowed values: 'Strong', 'BoundedStaleness', 'Session'.";
        var values = ParameterCoverageChecker.ParseAllowedValues(description);

        Assert.Equal(new[] { "Strong", "BoundedStaleness", "Session" }, values);
    }

    [Fact]
    public void ParseAllowedValues_ExampleOnlyPhrasing_ReturnsEmpty()
    {
        // "e.g." is an open-ended example, NOT a closed enum — must not be parsed as allowed values.
        const string description =
            "Filter recommendations by impacted Azure resource type (e.g., 'Microsoft.Storage/storageAccounts').";
        var values = ParameterCoverageChecker.ParseAllowedValues(description);

        Assert.Empty(values);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("A free-text parameter with no enumerated options.")]
    public void ParseAllowedValues_NoClosedEnum_ReturnsEmpty(string? description)
    {
        Assert.Empty(ParameterCoverageChecker.ParseAllowedValues(description));
    }

    // ── Enum coverage (positive) ────────────────────────────────────

    [Fact]
    public void EnumParam_PromptReferencesAllowedValue_ReturnsCovered()
    {
        // Advisor "recommendation apply": required param display name "Resource name",
        // enum option 'storage_storageaccounts'. Authoritative prompt says "Storage Account".
        var prompts = new[] { "Apply Advisor recommendations to a Terraform file for Storage Account" };
        const string description =
            "The Azure resource type for which to get rules to apply to IaaC file. Available options: "
            + "'aad_domainservices', 'keyvault_vaults', 'storage_storageaccounts', 'web_serverfarms', 'web_staticsites'.";

        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Resource name", 1, description);

        Assert.True(result.Covered,
            "Prompt referencing enum option 'Storage Account' should cover the enum-constrained 'Resource name' parameter");
    }

    [Fact]
    public void EnumParam_MultiSegmentResourceType_MatchesResourceTypeToken()
    {
        // AKS-style option 'containerservice_managedclusters'; prompt names the resource type only.
        var prompts = new[] { "Apply recommendations for a managed cluster to my Bicep file" };
        const string description =
            "The resource type to target. Available options: 'containerservice_managedclusters', 'compute_virtualmachines'.";

        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Resource name", 1, description);

        Assert.True(result.Covered,
            "Prompt naming the resource type 'managed cluster' should match option 'containerservice_managedclusters'");
    }

    [Fact]
    public void EnumParam_SingleWordEnumValue_MatchesWholeValue()
    {
        // Cosmos DB consistency level: single-token enum value.
        var prompts = new[] { "Create a Cosmos DB account with consistency BoundedStaleness" };
        const string description =
            "The default consistency level. Allowed values: 'Strong', 'BoundedStaleness', 'Session', 'Eventual'.";

        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "consistency-level", 1, description);

        Assert.True(result.Covered,
            "Prompt referencing enum value 'BoundedStaleness' should cover the parameter");
    }

    // ── Enum coverage (negative — must not blanket-pass) ────────────

    [Fact]
    public void EnumParam_PromptMissingAllowedValue_ReturnsFalse()
    {
        // Same enum param, but the prompt names no concrete option (generic ARM template).
        var prompts = new[] { "Apply Advisor recommendations to this ARM template" };
        const string description =
            "The Azure resource type for which to get rules to apply to IaaC file. Available options: "
            + "'aad_domainservices', 'keyvault_vaults', 'storage_storageaccounts', 'web_serverfarms', 'web_staticsites'.";

        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Resource name", 1, description);

        Assert.False(result.Covered,
            "Enum matching must not blanket-cover: a prompt naming no allowed option stays uncovered");
    }

    [Fact]
    public void EnumParam_NullDescription_BehavesLikeNonEnum()
    {
        // Without a description, an enum-style param with no name/value match stays uncovered
        // (proves enum matching is opt-in via description and introduces no false positives).
        var prompts = new[] { "Apply Advisor recommendations to a Terraform file for Storage Account" };

        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Resource name", 1, parameterDescription: null);

        Assert.False(result.Covered,
            "With no allowed-value description, coverage must fall back to name-based matching only");
    }

    [Fact]
    public void EnumDescription_DoesNotAffectAlreadyCoveredParameter()
    {
        // Additive guarantee: a parameter already covered by the name+value path stays covered
        // and is unaffected by enum parsing.
        var prompts = new[] { "Deploy to resource group named 'my-rg' for Storage Account" };
        const string description = "Available options: 'storage_storageaccounts', 'web_serverfarms'.";

        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "resource-group", 2, description);

        Assert.True(result.Covered);
    }

    [Fact]
    public void EnumParam_AllowedValueSegmentInsideUnrelatedWord_ReturnsFalse()
    {
        // Word-boundary guard: SQL option 'sql_servers' yields the generic candidate 'server'.
        // The prompt word "observer" *contains* "server" as a substring but is unrelated — a raw
        // substring scan of the collapsed prompt would wrongly mark the enum param covered, masking
        // a genuine miss. N-gram (whole-word) matching must keep this UNCOVERED.
        var prompts = new[] { "Set up an observer for diagnostic logs" };
        const string description =
            "The Azure resource type to target. Available options: 'sql_servers', 'storage_storageaccounts'.";

        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Resource name", 1, description);

        Assert.False(result.Covered,
            "Enum candidate 'server' must not match inside the unrelated word 'observer' (word-boundary matching)");
    }

    [Fact]
    public void EnumParam_ResourceTypeNamedAsWholeWord_ReturnsCovered()
    {
        // Positive counterpart to the boundary guard: when the prompt names the resource type as a
        // real word ("servers"), the same 'sql_servers' option is legitimately covered.
        var prompts = new[] { "Apply recommendations to my SQL servers" };
        const string description =
            "The Azure resource type to target. Available options: 'sql_servers', 'storage_storageaccounts'.";

        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "Resource name", 1, description);

        Assert.True(result.Covered,
            "Prompt naming the resource type 'servers' as a whole word should cover option 'sql_servers'");
    }

    // ── Phase 2: Generic suffix stripping for display names ──────────────────────

    [Theory]
    [InlineData("App name", "Start the web app <app>")]
    [InlineData("Resource group", "Deploy to resource <resource-group>")]
    [InlineData("Vault name", "List secrets in vault <vault>")]
    [InlineData("Account name", "Check account <account>")]
    public void PlaceholderDetected_WhenLastWordIsGenericSuffix_MatchesBaseWord(string paramName, string prompt)
    {
        var result = ParameterCoverageChecker.GetConcretePromptCoverage(
            new[] { prompt }, paramName, 1);

        Assert.True(result.PlaceholderDetected,
            $"Placeholder should be detected for '{paramName}' in: {prompt}");
    }

    [Fact]
    public void GenericSuffixList_CoversCommonCases()
    {
        // Each suffix should enable base-word-only matching
        foreach (var suffix in new[] { "group", "id", "name", "text", "value" })
        {
            var paramName = $"resource {suffix}";
            var prompt = "Check the resource <resource>";
            var result = ParameterCoverageChecker.GetConcretePromptCoverage(
                new[] { prompt }, paramName, 1);

            Assert.True(result.PlaceholderDetected,
                $"Placeholder should match for param 'resource {suffix}' with base word in placeholder");
        }
    }

    [Fact]
    public void NonGenericMultiWordParams_RequireFullMatch()
    {
        // "state change" — both words needed, "change" is NOT a generic suffix
        var prompts = new[] { "Check the current <state>" };

        var result = ParameterCoverageChecker.GetConcretePromptCoverage(prompts, "state change", 1);

        // "change" is not in the generic suffix list, so both words are needed
        // <state> only contains "state" not "change" — should NOT match with full confidence
        // (though individual word variants may still match depending on word length)
        // The key behavior: this is different from "App name" where "name" is generic
        Assert.False(result.Covered, "Non-generic suffix params should not get free coverage");
    }
}
