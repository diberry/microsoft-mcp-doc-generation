using FluentAssertions;
using SkillsGen.Core.Models;
using SkillsGen.Core.Validation;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Validation;

public class SkillPageValidatorTests
{
    private readonly SkillPageValidator _validator = new();

    private static SkillData CreateSkillData() => new()
    {
        Name = "azure-storage",
        DisplayName = "Azure Storage",
        Description = "Work with Azure Storage."
    };

    private static string CreateValidTier1Content() => """
        ---
        title: Azure skill for Azure Storage
        description: Work with Azure Storage accounts and resources.
        ---

        # Azure skill for Azure Storage

        Work with Azure Storage accounts and resources for blob containers and file shares.

        ## Prerequisites

        - GitHub Copilot with Azure extension
        - Azure subscription

        ### When to use this skill

        Use this skill when managing storage accounts and working with blob containers and queues and tables.

        ## What it provides

        The Azure Storage skill gives knowledge about storage services, blob operations, file shares, queues, and table storage access patterns in Azure.

        ## Example prompts

        - "How do I create a storage account?"
        - "List my blob containers"

        ## Related content

        - [Azure Storage documentation](/azure/storage)
        """;

    private static string CreateValidTier2Content() => """
        ---
        title: Azure skill for Azure Quotas
        description: Check Azure quotas.
        ---

        # Azure skill for Azure Quotas

        Check and manage your Azure subscription quotas and usage limits effectively with GitHub Copilot assistance today.

        ## Prerequisites

        - GitHub Copilot

        ### When to use this skill

        Use this skill to check quotas.

        ## What it provides

        Knowledge about Azure quotas and usage limits for your subscriptions and resources.

        ## Example prompts

        - "What are my current quotas?"

        ## Related content

        - [Azure quotas](/azure/quotas)
        """;

    [Fact]
    public void Validate_ValidTier1_ReturnsValid()
    {
        var content = CreateValidTier1Content();
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidTier2_ReturnsValid()
    {
        var content = CreateValidTier2Content();
        var result = _validator.Validate(content, 2, CreateSkillData(), new TriggerData([], [], null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_MissingPrerequisites_ReturnsError()
    {
        var content = """
            ---
            title: Azure skill for Test
            description: Test
            ---
            # Azure skill for Test

            Some content about this skill.

            ### When to use this skill

            Use it.

            ## What it provides

            Things.
            """;
        var result = _validator.Validate(content, 2, CreateSkillData(), new TriggerData([], [], null));

        result.Errors.Should().Contain(e => e.Contains("Prerequisites"));
    }

    [Fact]
    public void Validate_MissingFrontmatter_ReturnsError()
    {
        var content = "# Test\n\n## Prerequisites\n\n- GitHub Copilot\n\n## When to use\n\n## What it provides\n\nContent.";
        var result = _validator.Validate(content, 2, CreateSkillData(), new TriggerData([], [], null));

        result.Errors.Should().Contain(e => e.Contains("FRONTMATTER"));
    }

    [Fact]
    public void Validate_MissingTitleInFrontmatter_ReturnsError()
    {
        var content = "---\ndescription: Test\n---\n\n## Prerequisites\n\n- GitHub Copilot\n\n## When to use\n\n## What it provides";
        var result = _validator.Validate(content, 2, CreateSkillData(), new TriggerData([], [], null));

        result.Errors.Should().Contain(e => e.Contains("title"));
    }

    [Fact]
    public void Validate_EmptyContent_ReturnsError()
    {
        var result = _validator.Validate("", 1, CreateSkillData(), new TriggerData([], [], null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("EMPTY"));
    }

    [Fact]
    public void Validate_NoCopilot_ReturnsWarning()
    {
        var content = """
            ---
            title: Azure skill for Test
            description: Test content here
            ---

            Content without the required tool listed.

            ## Prerequisites

            - Azure CLI

            ### When to use this skill

            Use it.

            ## What it provides

            Knowledge about things.
            """;
        var result = _validator.Validate(content, 2, CreateSkillData(), new TriggerData([], [], null));

        result.Warnings.Should().Contain(w => w.Contains("PREREQ_COPILOT"));
    }

    [Fact]
    public void Validate_AbsoluteUrls_ReturnsWarning()
    {
        var content = """
            ---
            title: Azure skill for Test
            description: Test
            ---

            See https://learn.microsoft.com/azure/storage for info about GitHub Copilot.

            ## Prerequisites

            - GitHub Copilot

            ### When to use this skill

            Use it.

            ## What it provides

            Knowledge.
            """;
        var result = _validator.Validate(content, 2, CreateSkillData(), new TriggerData([], [], null));

        result.Warnings.Should().Contain(w => w.Contains("ACROLINX_URLS"));
    }

    [Fact]
    public void Validate_CountsSections()
    {
        var content = CreateValidTier1Content();
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.SectionCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Validate_DuplicateGitHubCopilotPrereq_ReturnsWarning()
    {
        var content = """
            ---
            title: Azure skill for Test
            description: Test skill
            ---

            # Azure skill for Test

            ## Prerequisites

            - **GitHub Copilot**—GitHub Copilot with the Azure extension enabled.

            ### Required tools

            - **GitHub Copilot**
            - **Azure CLI** (v2.60.0+)

            ### When to use this skill

            Use this skill to manage things.

            ## What it provides

            Knowledge about GitHub Copilot and Azure things.
            """;
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.Warnings.Should().Contain(w => w.Contains("PREREQ_DUPLICATE"));
    }

    [Fact]
    public void Validate_WorkWithFragmentInBullets_ReturnsWarning()
    {
        var content = """
            ---
            title: Azure skill for Test
            description: Test skill
            ---

            # Azure skill for Test

            ## Prerequisites

            - **GitHub Copilot**—Required.

            ### When to use this skill

            Use this skill when you need to:

            - Work with blob storage and file shares
            - Work with queues

            ## What it provides

            Knowledge about GitHub Copilot and storage.
            """;
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.Warnings.Should().Contain(w => w.Contains("FRAGMENT"));
    }

    [Fact]
    public void Validate_LinkTypo_ReturnsWarning()
    {
        var content = """
            ---
            title: Azure skill for Test
            description: Test skill
            ---

            # Azure skill for Test

            Learn more in the [GitHub Copilot docs](https://github-cilot-azure.com/docs).

            ## Prerequisites

            - GitHub Copilot

            ### When to use this skill

            Use this skill to manage things.

            ## What it provides

            Knowledge about GitHub Copilot and Azure things.
            """;
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.Warnings.Should().Contain(w => w.Contains("LINK_TYPO"));
    }

    [Fact]
    public void Validate_MicrosoftTypoInLink_ReturnsWarning()
    {
        var content = """
            ---
            title: Azure skill for Test
            description: Test skill
            ---

            # Azure skill for Test

            See [docs](https://learn.micosoft.com/azure/storage) for info.

            ## Prerequisites

            - GitHub Copilot

            ### When to use this skill

            Use this skill to check storage.

            ## What it provides

            Knowledge about GitHub Copilot and Azure storage.
            """;
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.Warnings.Should().Contain(w => w.Contains("LINK_TYPO"));
    }

    [Fact]
    public void Validate_CleanLinks_NoTypoWarning()
    {
        var content = CreateValidTier1Content();
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.Warnings.Should().NotContain(w => w.Contains("LINK_TYPO"));
    }

    [Fact]
    public void Validate_MissingExamplePrompts_ReturnsError()
    {
        var content = """
            ---
            title: Azure skill for Test
            description: Test
            ---
            # Azure skill for Test

            Some content about this skill with GitHub Copilot.

            ## Prerequisites

            - GitHub Copilot

            ### When to use this skill

            Use it.

            ## What it provides

            Things about stuff.

            ## Related content

            - [Docs](/azure/docs)
            """;
        var result = _validator.Validate(content, 2, CreateSkillData(), new TriggerData([], [], null));

        result.Errors.Should().Contain(e => e.Contains("Example prompts"));
    }

    [Fact]
    public void Validate_MissingRelatedContent_ReturnsError()
    {
        var content = """
            ---
            title: Azure skill for Test
            description: Test
            ---
            # Azure skill for Test

            Some content about this skill with GitHub Copilot.

            ## Prerequisites

            - GitHub Copilot

            ### When to use this skill

            Use it.

            ## What it provides

            Things about stuff.

            ## Example prompts

            - "How do I do things?"
            """;
        var result = _validator.Validate(content, 2, CreateSkillData(), new TriggerData([], [], null));

        result.Errors.Should().Contain(e => e.Contains("Related content"));
    }

    // §7.1: Internal MCP tool names (Azure__*) must never appear in customer-facing content (Issue #684, PR #9191)

    [Theory]
    [InlineData("Azure__documentation")]
    [InlineData("Azure__extension_cli_generate")]
    [InlineData("Azure__cost_analysis_query")]
    [InlineData("Azure__storage_blob_list")]
    public void Validate_InternalMcpToolNameInTable_ReturnsError(string toolName)
    {
        var content = $"""
            ---
            title: Azure skill for Test
            description: Test
            ---
            # Azure skill for Test

            Content about this skill with GitHub Copilot.

            ## Prerequisites

            - GitHub Copilot

            ### When to use this skill

            Use it.

            ## What it provides

            Knowledge about things.

            ## Example prompts

            - "How do I test?"
            - "Show me resources"
            - "What is available?"
            - "List my resources"
            - "Check my settings"

            ## Related content

            - [Docs](/azure/docs)

            ### Related tools

            | Tool | Command | Purpose |
            |------|---------|---------|
            | {toolName} | Do something | Perform an action |
            """;
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Azure__"),
            $"Validator must reject internal MCP tool name '{toolName}' in customer-facing content");
    }

    [Fact]
    public void Validate_InternalMcpToolNameInProse_ReturnsError()
    {
        var content = """
            ---
            title: Azure skill for Key Vault
            description: Manage Azure Key Vault secrets and keys.
            ---
            # Azure skill for Key Vault

            This skill uses Azure__keyvault_secret_get to retrieve secrets from GitHub Copilot.

            ## Prerequisites

            - GitHub Copilot

            ### When to use this skill

            Use it when managing secrets.

            ## What it provides

            Knowledge about Azure Key Vault.

            ## Example prompts

            - "How do I get a secret?"
            - "List my key vaults"
            - "Create a new secret"
            - "Rotate my keys"
            - "Check certificate expiry"

            ## Related content

            - [Key Vault docs](/azure/key-vault)
            """;
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Azure__"),
            "Validator must reject internal MCP tool names embedded in prose");
    }

    [Fact]
    public void Validate_AzureWordWithoutDoubleUnderscore_NoInternalToolError()
    {
        // "Azure Storage", "Azure Monitor", etc. are fine — only Azure__ is the smell
        var content = """
            ---
            title: Azure skill for Azure Monitor
            description: Monitor Azure resources.
            ---
            # Azure skill for Azure Monitor

            Use GitHub Copilot to monitor Azure resources including Azure Storage, Azure Monitor, and Azure SQL.

            ## Prerequisites

            - GitHub Copilot

            ### When to use this skill

            Use it when monitoring resources.

            ## What it provides

            Knowledge about Azure Monitor and Azure Storage.

            ## Example prompts

            - "Show my alerts"
            - "List metrics for my VM"
            - "Check log analytics"
            - "View my dashboards"
            - "Query my logs"

            ## Related content

            - [Azure Monitor docs](/azure/azure-monitor)
            """;
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.Errors.Should().NotContain(e => e.Contains("Azure__"),
            "Service names like 'Azure Storage' must not trigger the internal tool name check");
    }

    [Fact]
    public void Validate_CleanContent_NoInternalToolError()
    {
        var content = CreateValidTier1Content();
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.Errors.Should().NotContain(e => e.Contains("Azure__"),
            "Valid content with no internal tool names must not produce false positives");
    }

    // §7.1: '### Related tools' section heading exposes implementation details (Issue #684, PR #9191)

    [Fact]
    public void Validate_RelatedToolsSection_ReturnsError()
    {
        var content = """
            ---
            title: Azure skill for Cosmos DB
            description: Work with Azure Cosmos DB.
            ---
            # Azure skill for Cosmos DB

            Content about this skill with GitHub Copilot.

            ## Prerequisites

            - GitHub Copilot

            ### When to use this skill

            Use it when working with databases.

            ## What it provides

            Knowledge about Azure Cosmos DB.

            ## Example prompts

            - "List my databases"
            - "Create a container"
            - "Query my documents"
            - "Check throughput"
            - "View partition key"

            ## Related content

            - [Cosmos DB docs](/azure/cosmos-db)

            ### Related tools

            Some implementation details here.
            """;
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("POLICY") && e.Contains("Related tools"),
            "Validator must reject '### Related tools' heading as an implementation-detail leak");
    }

    [Theory]
    [InlineData("### Related tools")]
    [InlineData("### RELATED TOOLS")]
    [InlineData("### related tools")]
    [InlineData("### Related Tools")]
    public void Validate_RelatedToolsSection_CaseInsensitive_ReturnsError(string heading)
    {
        var content = $"""
            ---
            title: Azure skill for AKS
            description: Manage Azure Kubernetes Service.
            ---
            # Azure skill for AKS

            Content with GitHub Copilot.

            ## Prerequisites

            - GitHub Copilot

            ### When to use this skill

            Use it.

            ## What it provides

            Knowledge.

            ## Example prompts

            - "List my clusters"
            - "Scale my node pool"
            - "Check pod status"
            - "View namespaces"
            - "Get node info"

            ## Related content

            - [AKS docs](/azure/aks)

            {heading}

            Some tools listed here.
            """;
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("POLICY") && e.Contains("Related tools"),
            $"Case-insensitive match must catch '{heading}' as prohibited");
    }

    [Fact]
    public void Validate_RelatedContentSection_NoError()
    {
        // "## Related content" is the CORRECT section — must not be confused with "### Related tools"
        var content = CreateValidTier1Content();
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.Errors.Should().NotContain(e => e.Contains("Related tools"),
            "'## Related content' must not trigger the '### Related tools' prohibition");
    }

    // §7.1 + Issue #684 regression: combined scenario matching exact azure-cost output from PR #9191

    [Fact]
    public void Validate_AzureCostBugReproduction_BothErrorsPresent()
    {
        // Reproduces the exact output pattern that triggered Issue #684:
        // The pipeline emitted a '### Related tools' section containing Azure__* tool names.
        var content = """
            ---
            title: Azure skill for Azure Cost Management
            description: Analyze and manage Azure costs and budgets.
            ---
            # Azure skill for Azure Cost Management

            Analyze your Azure costs and manage budgets effectively with GitHub Copilot assistance.

            ## Prerequisites

            - GitHub Copilot with Azure extension

            ### When to use this skill

            Use this skill when you need to review costs, create budgets, or analyze spending trends.

            ## What it provides

            The Azure Cost Management skill provides knowledge about cost analysis, budgets, and billing.

            ## Example prompts

            - "What did I spend last month?"
            - "Show my top cost drivers"
            - "Create a budget alert"
            - "Break down costs by resource group"
            - "Forecast my Azure spending"

            ## Related content

            - [Azure Cost Management docs](/azure/cost-management-billing)

            ### Related tools

            | Tool | Command | Purpose |
            |------|---------|---------|
            | Azure__documentation | Search Azure documentation | Find relevant Azure docs |
            | Azure__extension_cli_generate | Generate Azure CLI commands | Build CLI scripts |
            """;
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.IsValid.Should().BeFalse("combined bug reproduction must fail validation");
        result.Errors.Should().Contain(e => e.Contains("POLICY") && e.Contains("Related tools"),
            "must catch the prohibited '### Related tools' heading");
        result.Errors.Should().Contain(e => e.Contains("Azure__"),
            "must catch internal MCP tool names Azure__documentation and Azure__extension_cli_generate");
    }

    [Fact]
    public void Validate_InternalToolNameInCodeBlock_ReturnsError()
    {
        // Tool names in backtick code spans must still be caught — they are still visible to readers
        var content = """
            ---
            title: Azure skill for SQL
            description: Work with Azure SQL Database.
            ---
            # Azure skill for Azure SQL

            Use GitHub Copilot with the `Azure__sql_query_execute` tool to query databases.

            ## Prerequisites

            - GitHub Copilot

            ### When to use this skill

            Use it when querying databases.

            ## What it provides

            Knowledge about Azure SQL.

            ## Example prompts

            - "Run a query"
            - "List my databases"
            - "Check connection"
            - "Show table schema"
            - "View query history"

            ## Related content

            - [Azure SQL docs](/azure/azure-sql)
            """;
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Azure__"),
            "Internal tool names inside backtick code spans must still be rejected");
    }

    [Fact]
    public void Validate_InternalToolNameMidSentence_ReturnsError()
    {
        // Embedded mid-sentence without any formatting
        var content = """
            ---
            title: Azure skill for App Service
            description: Deploy and manage Azure App Service.
            ---
            # Azure skill for App Service

            This skill internally routes requests to Azure__appservice_webapp_list and GitHub Copilot responds.

            ## Prerequisites

            - GitHub Copilot

            ### When to use this skill

            Use it to manage web apps.

            ## What it provides

            Knowledge about Azure App Service.

            ## Example prompts

            - "List my web apps"
            - "Scale my app service plan"
            - "Check deployment status"
            - "View logs"
            - "Create a new web app"

            ## Related content

            - [App Service docs](/azure/app-service)
            """;
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("Azure__"),
            "Internal tool names mid-sentence must be caught regardless of surrounding context");
    }

    // §7.1: Prohibited implementation-detail sections

    [Theory]
    [InlineData("## MCP tools")]
    [InlineData("## Sub-skills")]
    [InlineData("## Suggested workflow")]
    [InlineData("### Related tools")]
    public void Validate_ProhibitedSection_ReturnsError(string prohibitedSection)
    {
        var content = $"""
            ---
            title: Azure skill for Test
            description: Test
            ---
            # Azure skill for Test

            Content about this skill with GitHub Copilot.

            ## Prerequisites

            - GitHub Copilot

            ### When to use this skill

            Use it.

            ## What it provides

            Things.

            {prohibitedSection}

            Some prohibited content here.

            ## Example prompts

            - "How do I test?"

            ## Related content

            - [Docs](/azure/docs)
            """;
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("POLICY") && e.Contains("Prohibited"));
    }

    [Fact]
    public void Validate_McpToolNamePattern_ReturnsError()
    {
        var content = """
            ---
            title: Azure skill for Cost
            description: Manage Azure costs.
            ---
            # Azure skill for Cost

            Manage Azure costs with GitHub Copilot.

            ## Prerequisites

            - GitHub Copilot

            ### When to use this skill

            Use it to manage costs.

            ## What it provides

            This skill uses Azure__CostManagement_GetBudgets and Azure__CostManagement_ListAlerts internally.

            ## Example prompts

            - "What are my current costs?"
            - "Show me my budget alerts"
            - "List my spending by resource group"
            - "How do I set a budget alert?"
            - "What is my forecast for this month?"

            ## Related content

            - [Azure Cost Management](/azure/cost-management-billing)
            """;
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("POLICY") && e.Contains("Azure__"));
    }

    [Fact]
    public void Validate_CleanContent_NoMcpToolNameError()
    {
        var content = CreateValidTier1Content();
        var result = _validator.Validate(content, 1, CreateSkillData(), new TriggerData([], [], null));

        result.Errors.Should().NotContain(e => e.Contains("Azure__"));
    }
}
