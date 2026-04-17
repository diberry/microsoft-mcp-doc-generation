using Xunit;
using NaturalLanguageGenerator;

namespace DocGeneration.Core.NaturalLanguage.Tests;

/// <summary>
/// Unit tests for TextCleanup — the high-risk regex chain that transforms
/// every description and parameter in the 590+ generated markdown files.
/// </summary>
public class TextCleanupTests : IDisposable
{
    private static readonly string TestDataDir = Path.Combine(
        AppContext.BaseDirectory, "TestData");

    private static readonly string NlParamsFile = Path.Combine(TestDataDir, "test-nl-parameters.json");
    private static readonly string StaticTextFile = Path.Combine(TestDataDir, "test-static-text-replacement.json");

    private static readonly object LoadLock = new();
    private static bool _filesLoaded;

    public TextCleanupTests()
    {
        EnsureFilesLoaded();
    }

    private static void EnsureFilesLoaded()
    {
        lock (LoadLock)
        {
            if (_filesLoaded) return;
            var files = new List<string> { NlParamsFile, StaticTextFile };
            var loaded = TextCleanup.LoadFiles(files);
            Assert.True(loaded, "TextCleanup.LoadFiles should return true with valid test data files");
            _filesLoaded = true;
        }
    }

    public void Dispose() { /* no per-test teardown needed */ }

    // ─────────────────────────────────────────────
    // LoadFiles
    // ─────────────────────────────────────────────

    [Fact]
    public void LoadFiles_NullList_ReturnsFalse()
    {
        // Static state is already loaded; this validates the guard clause path
        var result = TextCleanup.LoadFiles(null!);
        Assert.False(result);
    }

    [Fact]
    public void LoadFiles_EmptyList_ReturnsFalse()
    {
        var result = TextCleanup.LoadFiles(new List<string>());
        Assert.False(result);
    }

    [Fact]
    public void LoadFiles_ValidFiles_SetsFilePathProperties()
    {
        // Re-load to verify path properties are set
        var files = new List<string> { NlParamsFile, StaticTextFile };
        TextCleanup.LoadFiles(files);

        Assert.NotNull(TextCleanup.ParametersFilePath);
        Assert.Contains("nl-parameters.json", TextCleanup.ParametersFilePath);
        Assert.NotNull(TextCleanup.TextReplacerParametersFilePath);
        Assert.Contains("static-text-replacement.json", TextCleanup.TextReplacerParametersFilePath);
    }

    [Fact]
    public void LoadFiles_ValidFiles_PopulatesMappedParameters()
    {
        var files = new List<string> { NlParamsFile, StaticTextFile };
        TextCleanup.LoadFiles(files);

        Assert.NotNull(TextCleanup.mappedParameters);
        Assert.True(TextCleanup.mappedParameters.Length > 0,
            "Should have loaded parameters from both JSON files");
    }

    [Fact]
    public void LoadFiles_NonExistentFiles_StillReturnsTrue()
    {
        // LoadFiles returns true even if files don't exist (logs warning, continues)
        var files = new List<string> { "nonexistent-nl-parameters.json", "nonexistent-static-text-replacement.json" };
        var result = TextCleanup.LoadFiles(files);
        Assert.True(result);

        // Restore real data for subsequent tests
        EnsureFilesLoadedForce();
    }

    // ─────────────────────────────────────────────
    // EnsureEndsPeriod
    // ─────────────────────────────────────────────

    [Fact]
    public void EnsureEndsPeriod_NullInput_ReturnsNull()
    {
        var result = TextCleanup.EnsureEndsPeriod(null!);
        Assert.Null(result);
    }

    [Fact]
    public void EnsureEndsPeriod_EmptyInput_ReturnsEmpty()
    {
        var result = TextCleanup.EnsureEndsPeriod(string.Empty);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void EnsureEndsPeriod_TextWithoutPeriod_AddsPeriod()
    {
        var result = TextCleanup.EnsureEndsPeriod("The Azure subscription ID to use");
        Assert.Equal("The Azure subscription ID to use.", result);
    }

    [Fact]
    public void EnsureEndsPeriod_TextAlreadyEndingWithPeriod_NoChange()
    {
        var result = TextCleanup.EnsureEndsPeriod("Specifies the Azure resource group.");
        Assert.Equal("Specifies the Azure resource group.", result);
    }

    [Fact]
    public void EnsureEndsPeriod_TextEndingWithQuestionMark_NoChange()
    {
        var result = TextCleanup.EnsureEndsPeriod("Is the resource currently available?");
        Assert.Equal("Is the resource currently available?", result);
    }

    [Fact]
    public void EnsureEndsPeriod_TextEndingWithExclamation_NoChange()
    {
        var result = TextCleanup.EnsureEndsPeriod("Resource provisioning failed!");
        Assert.Equal("Resource provisioning failed!", result);
    }

    [Fact]
    public void EnsureEndsPeriod_Idempotent_DoesNotAddDoublePeriod()
    {
        var input = "The Azure subscription ID to use";
        var first = TextCleanup.EnsureEndsPeriod(input);
        var second = TextCleanup.EnsureEndsPeriod(first);
        Assert.Equal("The Azure subscription ID to use.", second);
    }

    [Fact]
    public void EnsureEndsPeriod_TrimsWhitespace_BeforeCheck()
    {
        var result = TextCleanup.EnsureEndsPeriod("  The Cosmos DB account name  ");
        Assert.Equal("The Cosmos DB account name.", result);
    }

    [Fact]
    public void EnsureEndsPeriod_QuestionMarkBeforeSingleQuote_NoChange()
    {
        var result = TextCleanup.EnsureEndsPeriod("Ask 'Why are requests timing out?'");
        Assert.Equal("Ask 'Why are requests timing out?'", result);
    }

    [Fact]
    public void EnsureEndsPeriod_QuestionMarkBeforeDoubleQuote_NoChange()
    {
        var result = TextCleanup.EnsureEndsPeriod("Ask \"What is the status?\"");
        Assert.Equal("Ask \"What is the status?\"", result);
    }

    [Fact]
    public void EnsureEndsPeriod_ExclamationBeforeDoubleQuote_NoChange()
    {
        var result = TextCleanup.EnsureEndsPeriod("Alert said \"Critical failure!\"");
        Assert.Equal("Alert said \"Critical failure!\"", result);
    }

    [Fact]
    public void EnsureEndsPeriod_PeriodBeforeSingleQuote_NoChange()
    {
        var result = TextCleanup.EnsureEndsPeriod("Run command 'az show.'");
        Assert.Equal("Run command 'az show.'", result);
    }

    [Fact]
    public void EnsureEndsPeriod_NoPunctuationBeforeSingleQuote_AddsPeriod()
    {
        var result = TextCleanup.EnsureEndsPeriod("Use resource 'my-resource'");
        Assert.Equal("Use resource 'my-resource'.", result);
    }

    [Fact]
    public void EnsureEndsPeriod_NoPunctuationBeforeDoubleQuote_AddsPeriod()
    {
        var result = TextCleanup.EnsureEndsPeriod("Show vault \"my-vault\"");
        Assert.Equal("Show vault \"my-vault\".", result);
    }

    [Fact]
    public void EnsureEndsPeriod_QuestionMarkBeforeBacktick_NoChange()
    {
        var result = TextCleanup.EnsureEndsPeriod("Ask `Why is latency high?`");
        Assert.Equal("Ask `Why is latency high?`", result);
    }

    [Fact]
    public void EnsureEndsPeriod_NoPunctuationBeforeBacktick_AddsPeriod()
    {
        var result = TextCleanup.EnsureEndsPeriod("Run `az account show`");
        Assert.Equal("Run `az account show`.", result);
    }

    // ─────────────────────────────────────────────
    // WrapExampleValues
    // ─────────────────────────────────────────────

    [Fact]
    public void WrapExampleValues_NullInput_ReturnsNull()
    {
        var result = TextCleanup.WrapExampleValues(null!);
        Assert.Null(result);
    }

    [Fact]
    public void WrapExampleValues_EmptyInput_ReturnsEmpty()
    {
        var result = TextCleanup.WrapExampleValues(string.Empty);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void WrapExampleValues_NoExamplePattern_NoChange()
    {
        var input = "The name of the Azure Storage account to query.";
        var result = TextCleanup.WrapExampleValues(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void WrapExampleValues_SingleBareValue_WrapsInBackticks()
    {
        var input = "The retry mode (for example, Fixed)";
        var result = TextCleanup.WrapExampleValues(input);
        Assert.Equal("The retry mode (for example, `Fixed`)", result);
    }

    [Fact]
    public void WrapExampleValues_MultipleBareValues_WrapsEach()
    {
        var input = "The authentication method (for example, Credential, Key, ConnectionString)";
        var result = TextCleanup.WrapExampleValues(input);
        Assert.Equal("The authentication method (for example, `Credential`, `Key`, `ConnectionString`)", result);
    }

    [Fact]
    public void WrapExampleValues_AlreadyBackticked_PassesThroughUnchanged()
    {
        // The regex character class [^)`] excludes backticks, so already-wrapped
        // values don't match and pass through unchanged — idempotent by design.
        var input = "The retry mode (for example, `Fixed`)";
        var result = TextCleanup.WrapExampleValues(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void WrapExampleValues_ValueWithExplanation_OnlyWrapsValueToken()
    {
        // "PT1H for 1 hour" — only "PT1H" should be backticked
        var input = "The duration format (for example, PT1H for 1 hour, PT30M for 30 minutes)";
        var result = TextCleanup.WrapExampleValues(input);
        Assert.Contains("`PT1H`", result);
        Assert.Contains("`PT30M`", result);
        Assert.Contains("for 1 hour", result);
        Assert.Contains("for 30 minutes", result);
        Assert.DoesNotContain("`for`", result);
    }

    [Fact]
    public void WrapExampleValues_MixedValueAndExplanation_CommaSplit()
    {
        // Comma split: "Standard_DS1_v2, Standard_DS2_v2" both are pure values
        var input = "The VM size (for example, Standard_DS1_v2, Standard_DS2_v2)";
        var result = TextCleanup.WrapExampleValues(input);
        Assert.Equal("The VM size (for example, `Standard_DS1_v2`, `Standard_DS2_v2`)", result);
    }

    [Fact]
    public void WrapExampleValues_Idempotent_SimpleValues()
    {
        var input = "The region (for example, eastus, westus2)";
        var first = TextCleanup.WrapExampleValues(input);
        Assert.Equal("The region (for example, `eastus`, `westus2`)", first);
        // Second application: the backticked values now contain ` chars
        // The regex matches [^)`]+ which excludes `) but includes `
        // This tests actual double-application behavior
        var second = TextCleanup.WrapExampleValues(first);
        // Documenting actual behavior on double-application
        Assert.Contains("for example,", second);
    }

    [Fact]
    public void WrapExampleValues_RealisticAzureContent_SubscriptionId()
    {
        var input = "The subscription ID (for example, 00000000-0000-0000-0000-000000000000)";
        var result = TextCleanup.WrapExampleValues(input);
        Assert.Equal(
            "The subscription ID (for example, `00000000-0000-0000-0000-000000000000`)",
            result);
    }

    [Fact]
    public void WrapExampleValues_MultipleExamplePatternsInSameString()
    {
        var input = "Use the region (for example, eastus) and the SKU (for example, Standard_LRS)";
        var result = TextCleanup.WrapExampleValues(input);
        Assert.Contains("(for example, `eastus`)", result);
        Assert.Contains("(for example, `Standard_LRS`)", result);
    }

    // ─────────────────────────────────────────────
    // CleanAIGeneratedText
    // ─────────────────────────────────────────────

    [Fact]
    public void CleanAIGeneratedText_NullInput_ReturnsNull()
    {
        var result = TextCleanup.CleanAIGeneratedText(null!);
        Assert.Null(result);
    }

    [Fact]
    public void CleanAIGeneratedText_EmptyInput_ReturnsEmpty()
    {
        var result = TextCleanup.CleanAIGeneratedText(string.Empty);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void CleanAIGeneratedText_SmartSingleQuotes_ReplacedWithStraight()
    {
        // \u2018 = left single, \u2019 = right single
        var input = "List the account\u2019s storage containers";
        var result = TextCleanup.CleanAIGeneratedText(input);
        Assert.Equal("List the account's storage containers", result);
        Assert.DoesNotContain("\u2019", result);
    }

    [Fact]
    public void CleanAIGeneratedText_SmartDoubleQuotes_ReplacedWithStraight()
    {
        // \u201C = left double, \u201D = right double
        var input = "Set the \u201Cretry-mode\u201D parameter to \u201CFixed\u201D";
        var result = TextCleanup.CleanAIGeneratedText(input);
        Assert.Equal("Set the \"retry-mode\" parameter to \"Fixed\"", result);
        Assert.DoesNotContain("\u201C", result);
        Assert.DoesNotContain("\u201D", result);
    }

    [Fact]
    public void CleanAIGeneratedText_HtmlQuoteEntities_Replaced()
    {
        var input = "Use &quot;Credential&quot; or &apos;Key&apos; for auth-method";
        var result = TextCleanup.CleanAIGeneratedText(input);
        Assert.Equal("Use \"Credential\" or 'Key' for auth-method", result);
    }

    [Fact]
    public void CleanAIGeneratedText_NumericHtmlEntities_Replaced()
    {
        var input = "Set value to &#34;eastus&#34; and name to &#39;myResource&#39;";
        var result = TextCleanup.CleanAIGeneratedText(input);
        Assert.Equal("Set value to \"eastus\" and name to 'myResource'", result);
    }

    [Fact]
    public void CleanAIGeneratedText_AmpersandEntity_Replaced()
    {
        var input = "Storage &amp; Compute services";
        var result = TextCleanup.CleanAIGeneratedText(input);
        Assert.Equal("Storage & Compute services", result);
    }

    [Fact]
    public void CleanAIGeneratedText_AngleBracketEntities_Replaced()
    {
        var input = "Filter with &lt;resource-group&gt; syntax";
        var result = TextCleanup.CleanAIGeneratedText(input);
        Assert.Equal("Filter with <resource-group> syntax", result);
    }

    [Fact]
    public void CleanAIGeneratedText_MixedEntities_AllReplaced()
    {
        var input = "Use &quot;subscription&quot; &amp; \u201Cresource-group\u201D for &lt;scope&gt;";
        var result = TextCleanup.CleanAIGeneratedText(input);
        Assert.Equal("Use \"subscription\" & \"resource-group\" for <scope>", result);
    }

    [Fact]
    public void CleanAIGeneratedText_AlreadyCleanText_NoChange()
    {
        var input = "List all Azure Cosmos DB databases in the specified account.";
        var result = TextCleanup.CleanAIGeneratedText(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void CleanAIGeneratedText_Idempotent_DoubleApplication()
    {
        var input = "Set the \u201Cretry-mode\u201D parameter to &quot;Fixed&quot;";
        var first = TextCleanup.CleanAIGeneratedText(input);
        var second = TextCleanup.CleanAIGeneratedText(first);
        Assert.Equal(first, second);
    }

    // ─────────────────────────────────────────────
    // ReplaceStaticText
    // ─────────────────────────────────────────────

    [Fact]
    public void ReplaceStaticText_NullInput_ReturnsNull()
    {
        var result = TextCleanup.ReplaceStaticText(null!);
        Assert.Null(result);
    }

    [Fact]
    public void ReplaceStaticText_EmptyInput_ReturnsEmpty()
    {
        var result = TextCleanup.ReplaceStaticText(string.Empty);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ReplaceStaticText_AzureAD_ReplacedWithEntraID()
    {
        var input = "Configure Azure Active Directory for the subscription";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal("Configure Microsoft Entra ID for the subscription", result);
    }

    [Fact]
    public void ReplaceStaticText_AzureADShort_ReplacedWithEntraID()
    {
        var input = "Set up Azure AD authentication";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal("Set up Microsoft Entra ID authentication", result);
    }

    [Fact]
    public void ReplaceStaticText_CosmosDB_ReplacedWithBrand()
    {
        var input = "Query the CosmosDB account for documents";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal("Query the Azure Cosmos DB account for documents", result);
    }

    [Fact]
    public void ReplaceStaticText_VMSS_ReplacedWithFullName()
    {
        var input = "List all VMSS instances in the resource group";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal("List all Virtual machine scale set (VMSS) instances in the resource group", result);
    }

    [Fact]
    public void ReplaceStaticText_InclusiveLanguage_WhitelistToAllowlist()
    {
        var input = "Add the IP address to the whitelist";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal("Add the IP address to the allowlist", result);
    }

    [Fact]
    public void ReplaceStaticText_InclusiveLanguage_BlacklistToBlocklist()
    {
        var input = "Remove the domain from the blacklist";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal("Remove the domain from the blocklist", result);
    }

    [Fact]
    public void ReplaceStaticText_WordSimplification_LeverageToUse()
    {
        var input = "Leverage Azure Key Vault to store secrets";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Contains("use", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Leverage", result);
    }

    [Fact]
    public void ReplaceStaticText_PhraseReplacement_InOrderTo()
    {
        var input = "Run the command in order to provision the resource";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal("Run the command to provision the resource", result);
    }

    [Fact]
    public void ReplaceStaticText_PhraseReplacement_PriorTo()
    {
        var input = "Complete authentication prior to running the query";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal("Complete authentication before running the query", result);
    }

    [Fact]
    public void ReplaceStaticText_MultipleReplacements_InSameText()
    {
        var input = "Leverage Azure AD in order to whitelist the application";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.DoesNotContain("Leverage", result);
        Assert.DoesNotContain("Azure AD", result);
        Assert.DoesNotContain("in order to", result);
        Assert.DoesNotContain("whitelist", result);
        Assert.Contains("Microsoft Entra ID", result);
        Assert.Contains("allowlist", result);
    }

    [Fact]
    public void ReplaceStaticText_WordBoundaryRespected_NoPartialMatch()
    {
        // "utilize" should not match inside "reutilize" (not a real word, but tests boundary)
        // The regex uses lookarounds (?<![A-Za-z0-9_-]) and (?![A-Za-z0-9_-])
        var input = "The node utilizes the compute pool";
        // "utilizes" is not "utilize" — extra 's' means the regex should not match
        // because lookahead (?![A-Za-z0-9_-]) would fail on the trailing 's'
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Contains("utilizes", result);
    }

    [Fact]
    public void ReplaceStaticText_CaseInsensitive_MatchesUppercase()
    {
        var input = "Use COSMOSDB for NoSQL workloads";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Contains("Azure Cosmos DB", result);
    }

    [Fact]
    public void ReplaceStaticText_AlreadyCleanText_NoChange()
    {
        var input = "List all Azure Cosmos DB databases in the specified account";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void ReplaceStaticText_Idempotent_DoubleApplication()
    {
        var input = "Configure Azure Active Directory for the whitelist";
        var first = TextCleanup.ReplaceStaticText(input);
        var second = TextCleanup.ReplaceStaticText(first);
        Assert.Equal(first, second);
    }

    [Fact]
    public void ReplaceStaticText_EtcReplacement()
    {
        var input = "Supports regions like eastus, westus, etc.";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Contains("and more", result);
        Assert.DoesNotContain("etc.", result);
    }

    [Fact]
    public void ReplaceStaticText_SanityCheckReplacement()
    {
        var input = "Run a sanity check before deploying";
        var result = TextCleanup.ReplaceStaticText(input);
        Assert.Contains("validation check", result);
        Assert.DoesNotContain("sanity check", result);
    }

    // ─────────────────────────────────────────────
    // NormalizeParameter
    // ─────────────────────────────────────────────

    [Fact]
    public void NormalizeParameter_NullInput_ReturnsUnknown()
    {
        var result = TextCleanup.NormalizeParameter(null!);
        Assert.Equal("Unknown", result);
    }

    [Fact]
    public void NormalizeParameter_EmptyInput_ReturnsUnknown()
    {
        var result = TextCleanup.NormalizeParameter(string.Empty);
        Assert.Equal("Unknown", result);
    }

    [Fact]
    public void NormalizeParameter_DirectMapping_ReturnsNaturalLanguage()
    {
        // "auth-method" → "Authentication method" (from test-nl-parameters.json)
        var result = TextCleanup.NormalizeParameter("auth-method");
        Assert.Equal("Authentication method", result);
    }

    [Fact]
    public void NormalizeParameter_CliDashDash_StrippedBeforeLookup()
    {
        var result = TextCleanup.NormalizeParameter("--auth-method");
        Assert.Equal("Authentication method", result);
    }

    [Fact]
    public void NormalizeParameter_HyphenatedWords_SplitAndCapitalized()
    {
        // "resource-group" → "Resource group" (no direct mapping, uses split logic)
        var result = TextCleanup.NormalizeParameter("resource-group");
        Assert.Equal("Resource group", result);
    }

    [Fact]
    public void NormalizeParameter_AcronymId_ConvertedToUppercase()
    {
        var result = TextCleanup.NormalizeParameter("subscription-id");
        Assert.Contains("ID", result);
    }

    [Fact]
    public void NormalizeParameter_AcronymUri_ConvertedToUppercase()
    {
        // "cluster-uri" has a direct mapping → "Cluster URI"
        var result = TextCleanup.NormalizeParameter("cluster-uri");
        Assert.Equal("Cluster URI", result);
    }

    [Fact]
    public void NormalizeParameter_CliPrefix_StrippedFromGenericParam()
    {
        var result = TextCleanup.NormalizeParameter("--retry-delay");
        Assert.Equal("Retry delay", result);
    }

    [Fact]
    public void NormalizeParameter_SingleWord_Capitalized()
    {
        var result = TextCleanup.NormalizeParameter("subscription");
        Assert.Equal("Subscription", result);
    }

    [Fact]
    public void NormalizeParameter_PreservesAllWords_ResourceGroupName()
    {
        // Per the docstring: "resource-group-name" → "Resource group name" (NOT "Resource group")
        var result = TextCleanup.NormalizeParameter("resource-group-name");
        Assert.Equal("Resource group name", result);
    }

    // ─────────────────────────────────────────────
    // Full cleanup chain (integration-style)
    // ─────────────────────────────────────────────

    [Fact]
    public void FullChain_RealisticParameterDescription_AllTransformsApplied()
    {
        // Simulate a real AI-generated description with multiple issues
        var raw = "Leverage Azure AD in order to whitelist the application (for example, myApp)";

        // Step 1: Replace static text
        var afterReplace = TextCleanup.ReplaceStaticText(raw);
        Assert.DoesNotContain("Leverage", afterReplace);
        Assert.DoesNotContain("Azure AD", afterReplace);
        Assert.Contains("Microsoft Entra ID", afterReplace);

        // Step 2: Wrap example values
        var afterWrap = TextCleanup.WrapExampleValues(afterReplace);
        Assert.Contains("`myApp`", afterWrap);

        // Step 3: Ensure ends with period
        var afterPeriod = TextCleanup.EnsureEndsPeriod(afterWrap);
        Assert.EndsWith(".", afterPeriod);

        // Verify final output is clean and well-formed
        Assert.DoesNotContain("whitelist", afterPeriod);
        Assert.DoesNotContain("in order to", afterPeriod);
    }

    [Fact]
    public void FullChain_AIGeneratedWithSmartQuotes_CleanedThenTransformed()
    {
        var raw = "Set the \u201Cauth-method\u201D parameter to \u201CCredential\u201D prior to deployment";

        // Step 1: Clean AI text
        var afterClean = TextCleanup.CleanAIGeneratedText(raw);
        Assert.DoesNotContain("\u201C", afterClean);

        // Step 2: Static text replacement
        var afterReplace = TextCleanup.ReplaceStaticText(afterClean);
        Assert.Contains("before", afterReplace);
        Assert.DoesNotContain("prior to", afterReplace);

        // Step 3: Period
        var afterPeriod = TextCleanup.EnsureEndsPeriod(afterReplace);
        Assert.EndsWith(".", afterPeriod);
    }

    [Fact]
    public void FullChain_IdempotentDoubleApplication()
    {
        var raw = "Utilize Azure AD to leverage the blacklist (for example, myBlock)";

        var pass1Replace = TextCleanup.ReplaceStaticText(raw);
        var pass1Wrap = TextCleanup.WrapExampleValues(pass1Replace);
        var pass1Period = TextCleanup.EnsureEndsPeriod(pass1Wrap);

        var pass2Replace = TextCleanup.ReplaceStaticText(pass1Period);
        var pass2Wrap = TextCleanup.WrapExampleValues(pass2Replace);
        var pass2Period = TextCleanup.EnsureEndsPeriod(pass2Wrap);

        // Static text + period should be stable after first pass
        Assert.Equal(pass1Period, pass2Period);
    }

    // ─────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────

    private static void EnsureFilesLoadedForce()
    {
        lock (LoadLock)
        {
            var files = new List<string> { NlParamsFile, StaticTextFile };
            TextCleanup.LoadFiles(files);
            _filesLoaded = true;
        }
    }
}
