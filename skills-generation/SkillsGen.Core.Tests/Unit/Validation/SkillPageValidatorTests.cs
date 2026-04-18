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
}
