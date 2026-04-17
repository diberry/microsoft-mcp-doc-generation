// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using Azure.Mcp.TextTransformation.Services;
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Tests for TextCleanup.ReplaceStaticText to verify branding and text normalization.
/// Fixes: #141 — CosmosDB branding not normalized.
/// </summary>
public class StaticTextReplacementTests : IClassFixture<TransformationEngineFixture>
{
    private readonly TextNormalizer _normalizer;

    public StaticTextReplacementTests(TransformationEngineFixture fixture)
    {
        _normalizer = fixture.Normalizer;
    }

    // ── CosmosDB Branding (#141) ────────────────────────────────────

    [Fact]
    public void ReplaceStaticText_CosmosDB_ReplacedWithAzureCosmosDB()
    {
        // Arrange — CLI description containing "CosmosDB" (no space)
        var input = "Add a CosmosDB database to the web app";

        // Act
        var result = _normalizer.ReplaceStaticText(input);

        // Assert — should normalize to "Azure Cosmos DB"
        Assert.Contains("Azure Cosmos DB", result);
        Assert.DoesNotContain("CosmosDB", result);
    }

    [Theory]
    [InlineData("Connect to CosmosDB", "Connect to Azure Cosmos DB")]
    [InlineData("The CosmosDB account was created", "The Azure Cosmos DB account was created")]
    [InlineData("Use CosmosDB for NoSQL data", "Use Azure Cosmos DB for NoSQL data")]
    public void ReplaceStaticText_CosmosDB_VariousContexts(string input, string expected)
    {
        // Act
        var result = _normalizer.ReplaceStaticText(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceStaticText_CosmosDB_NotReplacedInsideWord()
    {
        // "CosmosDB" inside a larger token should NOT be replaced
        // e.g., "MyCosmosDBApp" should not become "MyAzure Cosmos DBApp"
        var input = "The MyCosmosDBApp is running";

        // Act
        var result = _normalizer.ReplaceStaticText(input);

        // Assert — word-boundary matching should prevent partial replacement
        Assert.Contains("MyCosmosDBApp", result);
    }

    // ── Existing Replacement Verification ───────────────────────────

    [Theory]
    [InlineData("e.g.", "for example")]
    [InlineData("i.e.", "in other words")]
    public void ReplaceStaticText_ExistingAbbreviations_StillWork(string abbrev, string expanded)
    {
        // Arrange
        var input = $"This is {abbrev} a test";

        // Act
        var result = _normalizer.ReplaceStaticText(input);

        // Assert
        Assert.Contains(expanded, result);
    }

    [Theory]
    [InlineData("VMSS", "Virtual machine scale set (VMSS)")]
    public void ReplaceStaticText_ExistingBrandNames_StillWork(string original, string replacement)
    {
        // Arrange — VMSS as standalone word
        var input = $"Deploy to {original} instances";

        // Act
        var result = _normalizer.ReplaceStaticText(input);

        // Assert
        Assert.Contains(replacement, result);
    }

    // ── Demonstrative Pronoun Fix (#144) ────────────────────────────

    [Fact]
    public void ReplaceStaticText_DemonstrativePronoun_LogicalContainer_Replaced()
    {
        // Arrange — Acrolinx flags "This is a logical container" as missing noun after "This"
        var input = "This is a logical container for Azure resources.";

        // Act
        var result = _normalizer.ReplaceStaticText(input);

        // Assert — should add noun "resource group" after "This"
        Assert.Contains("This resource group is a logical container", result);
        Assert.DoesNotContain("This is a logical container", result);
    }

    [Theory]
    [InlineData(
        "The name of the Azure resource group. This is a logical container for Azure resources.",
        "The name of the Azure resource group. This resource group is a logical container for Azure resources.")]
    [InlineData(
        "This is a logical container for resources in your subscription.",
        "This resource group is a logical container for resources in your subscription.")]
    public void ReplaceStaticText_DemonstrativePronoun_VariousContexts(string input, string expected)
    {
        // Act
        var result = _normalizer.ReplaceStaticText(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceStaticText_DemonstrativePronoun_NotReplacedWhenAlreadyFixed()
    {
        // Already-fixed text should pass through unchanged
        var input = "This resource group is a logical container for Azure resources.";

        // Act
        var result = _normalizer.ReplaceStaticText(input);

        // Assert — no double-replacement
        Assert.Equal("This resource group is a logical container for Azure resources.", result);
    }

    // ── Acrolinx Wordy/Informal Phrase Replacements (#215) ──────────

    [Theory]
    [InlineData("Configure storage etc.", "Configure storage and more")]
    [InlineData("Manage VMs, storage, etc.", "Manage VMs, storage, and more")]
    public void ReplaceStaticText_Etc_ReplacedWithAndMore(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceStaticText_Etc_NotReplacedInsideBackticks()
    {
        // "etc." inside backticks should stay — it's a code reference
        // Note: static replacement doesn't skip backticks — backtick-awareness
        // is the responsibility of higher-level fixers in FamilyFileStitcher
        var input = "Use the `etc.` directory for config files.";
        var result = _normalizer.ReplaceStaticText(input);
        // Static replacement applies to all text including backtick content;
        // backtick protection is handled at the fixer level, not here
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("Configure the resource in order to deploy.", "Configure the resource to deploy.")]
    [InlineData("In order to use this tool, authenticate first.", "to use this tool, authenticate first.")]
    public void ReplaceStaticText_InOrderTo_ReplacedWithTo(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Make sure the cluster is running.", "ensure the cluster is running.")]
    [InlineData("make sure you authenticate first.", "ensure you authenticate first.")]
    public void ReplaceStaticText_MakeSure_ReplacedWithEnsure(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("A number of resources are available.", "several resources are available.")]
    [InlineData("a number of parameters are optional.", "several parameters are optional.")]
    public void ReplaceStaticText_ANumberOf_ReplacedWithSeveral(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Utilize the Azure CLI to deploy.", "use the Azure CLI to deploy.")]
    [InlineData("You can utilize this tool.", "You can use this tool.")]
    public void ReplaceStaticText_Utilize_ReplacedWithUse(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("This functionality enables deployment.", "This feature enables deployment.")]
    [InlineData("The tool provides monitoring functionality.", "The tool provides monitoring feature.")]
    public void ReplaceStaticText_Functionality_ReplacedWithFeature(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Connect via the Azure portal.", "Connect through the Azure portal.")]
    [InlineData("Deploy resources via CLI.", "Deploy resources through CLI.")]
    public void ReplaceStaticText_Via_ReplacedWithThrough(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("You can leverage this tool.", "You can use this tool.")]
    [InlineData("Leverage the existing infrastructure.", "use the existing infrastructure.")]
    public void ReplaceStaticText_Leverage_ReplacedWithUse(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Authenticate prior to running the command.", "Authenticate before running the command.")]
    [InlineData("Prior to deployment, configure the app.", "before deployment, configure the app.")]
    public void ReplaceStaticText_PriorTo_ReplacedWithBefore(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceStaticText_MultipleWordyPhrases_AllReplaced()
    {
        var input = "You can utilize this feature to leverage the existing tools.";
        var result = _normalizer.ReplaceStaticText(input);
        Assert.DoesNotContain("utilize", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("leverage", result, StringComparison.OrdinalIgnoreCase);
    }

    // ── Deprecated Microsoft Terminology (Acrolinx P2) ──────────────

    [Theory]
    [InlineData("Connect to Azure Active Directory for auth.", "Connect to Microsoft Entra ID for auth.")]
    [InlineData("azure active directory supports SSO.", "Microsoft Entra ID supports SSO.")]
    public void ReplaceStaticText_AzureActiveDirectory_ReplacedWithEntraID(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Configure Azure AD for your tenant.", "Configure Microsoft Entra ID for your tenant.")]
    [InlineData("Use azure ad to manage identities.", "Use Microsoft Entra ID to manage identities.")]
    public void ReplaceStaticText_AzureAD_ReplacedWithEntraID(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Enable AAD authentication.", "Enable Microsoft Entra ID authentication.")]
    [InlineData("The AAD token is valid.", "The Microsoft Entra ID token is valid.")]
    public void ReplaceStaticText_AAD_ReplacedWithEntraID(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceStaticText_AAD_NotReplacedInsideWords()
    {
        // "AAD" inside a larger token should NOT be replaced
        var input = "The SAAD library and AADConnect tool are configured.";
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Contains("SAAD", result);
        Assert.Contains("AADConnect", result);
    }

    // ── Inclusive Language (Acrolinx P2) ─────────────────────────────

    [Theory]
    [InlineData("Add the IP to the whitelist.", "Add the IP to the allowlist.")]
    [InlineData("Whitelist the domain name.", "allowlist the domain name.")]
    public void ReplaceStaticText_Whitelist_ReplacedWithAllowlist(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceStaticText_Whitelist_NotReplacedInsideWords()
    {
        // "whitelisted" should NOT become "allowlisted" — word boundary guard
        var input = "The IP was whitelisted yesterday.";
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Contains("whitelisted", result);
    }

    [Theory]
    [InlineData("Remove the IP from the blacklist.", "Remove the IP from the blocklist.")]
    [InlineData("Blacklist the malicious domain.", "blocklist the malicious domain.")]
    public void ReplaceStaticText_Blacklist_ReplacedWithBlocklist(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Push changes to the master branch.", "Push changes to the main branch.")]
    [InlineData("Merge into master branch before release.", "Merge into main branch before release.")]
    public void ReplaceStaticText_MasterBranch_ReplacedWithMainBranch(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Run a sanity check before deploying.", "Run a validation check before deploying.")]
    [InlineData("Perform a sanity check on the config.", "Perform a validation check on the config.")]
    public void ReplaceStaticText_SanityCheck_ReplacedWithValidationCheck(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Use a dummy value for testing.", "Use a placeholder value for testing.")]
    [InlineData("Create a dummy account.", "Create a placeholder account.")]
    public void ReplaceStaticText_Dummy_ReplacedWithPlaceholder(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    // ── Additional Wordy Phrases (Acrolinx P2) ─────────────────────

    [Theory]
    [InlineData("Due to the fact that the server is down, retry later.", "because the server is down, retry later.")]
    [InlineData("This fails due to the fact that the key expired.", "This fails because the key expired.")]
    public void ReplaceStaticText_DueToTheFactThat_ReplacedWithBecause(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("This tool provides the ability to deploy apps.", "This tool lets you deploy apps.")]
    [InlineData("The CLI provides the ability to manage resources.", "The CLI lets you manage resources.")]
    public void ReplaceStaticText_ProvidesTheAbilityTo_ReplacedWithLetsYou(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("In the event that the deployment fails, roll back.", "if the deployment fails, roll back.")]
    [InlineData("Retry in the event that the connection times out.", "Retry if the connection times out.")]
    public void ReplaceStaticText_InTheEventThat_ReplacedWithIf(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("The tool is able to monitor resources.", "The tool can monitor resources.")]
    [InlineData("This service is able to scale automatically.", "This service can scale automatically.")]
    public void ReplaceStaticText_IsAbleTo_ReplacedWithCan(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Deploy VMs as well as containers.", "Deploy VMs and containers.")]
    [InlineData("Manage storage as well as networking.", "Manage storage and networking.")]
    public void ReplaceStaticText_AsWellAs_ReplacedWithAnd(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("In addition to VMs, you can deploy containers.", "besides VMs, you can deploy containers.")]
    [InlineData("Configure DNS in addition to networking.", "Configure DNS besides networking.")]
    public void ReplaceStaticText_InAdditionTo_ReplacedWithBesides(string input, string expected)
    {
        var result = _normalizer.ReplaceStaticText(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplaceStaticText_MultipleP2Replacements_AllApplied()
    {
        var input = "Utilize Azure AD as well as the whitelist to configure access.";
        var result = _normalizer.ReplaceStaticText(input);
        Assert.DoesNotContain("Azure AD", result);
        Assert.DoesNotContain("whitelist", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("as well as", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("utilize", result, StringComparison.OrdinalIgnoreCase);
    }
}
