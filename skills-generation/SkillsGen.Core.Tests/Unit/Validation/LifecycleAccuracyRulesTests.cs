using FluentAssertions;
using SkillsGen.Core.Models;
using SkillsGen.Core.Validation;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Validation;

/// <summary>
/// Tests for the universal (service-agnostic) doc-accuracy rules covering issues
/// #734 (phase-aware verbs), #735 (user-facing phrasing), and #737 (title fidelity).
/// Test data deliberately spans many Azure services (Storage, Key Vault, Cosmos DB,
/// Speech, Monitor, AKS) to guard against service-specific logic.
/// </summary>
public class LifecycleAccuracyRulesTests
{
    // ---------- #734: DetectPhase ----------

    [Fact]
    public void DetectPhase_AuthoringSkill_ReturnsAuthor()
    {
        var skill = new SkillData
        {
            Name = "azure-prepare",
            DisplayName = "Azure Prepare",
            Description = "Generates Bicep infrastructure and a deployment plan for your app.",
            RawBody = "This skill generates infrastructure as code. Provisioning happens later, in the azure-deploy skill."
        };

        LifecycleAccuracyRules.DetectPhase(skill).Should().Be(SkillLifecyclePhase.Author);
    }

    [Fact]
    public void DetectPhase_DeploySkill_ReturnsDeploy()
    {
        var skill = new SkillData
        {
            Name = "azure-deploy",
            DisplayName = "Azure Deploy",
            Description = "Executes the deployment and provisions Azure resources.",
            RawBody = "Runs azd up to execute the deployment to Azure Container Apps."
        };

        LifecycleAccuracyRules.DetectPhase(skill).Should().Be(SkillLifecyclePhase.Deploy);
    }

    [Fact]
    public void DetectPhase_ValidateSkill_ReturnsValidate()
    {
        var skill = new SkillData
        {
            Name = "azure-validate",
            DisplayName = "Azure Validate",
            Description = "Runs preflight checks on your Key Vault deployment.",
            RawBody = "Validates the infrastructure before you deploy."
        };

        LifecycleAccuracyRules.DetectPhase(skill).Should().Be(SkillLifecyclePhase.Validate);
    }

    [Fact]
    public void DetectPhase_NoSignals_ReturnsUnknown()
    {
        var skill = new SkillData
        {
            Name = "azure-cosmos",
            DisplayName = "Azure Cosmos DB",
            Description = "Query and manage Cosmos DB containers.",
            RawBody = "Knowledge about Cosmos DB request units and partition keys."
        };

        LifecycleAccuracyRules.DetectPhase(skill).Should().Be(SkillLifecyclePhase.Unknown);
    }

    [Fact]
    public void DetectPhase_Null_ReturnsUnknown()
    {
        LifecycleAccuracyRules.DetectPhase(null!).Should().Be(SkillLifecyclePhase.Unknown);
    }

    // ---------- #734: FindForbiddenPhaseVerbs ----------

    [Fact]
    public void FindForbiddenPhaseVerbs_AuthorPhaseClaimsProvisioning_Flags()
    {
        const string prose =
            "The Azure Speech skill provisions the speech service and creates resource groups for you.";

        var hits = LifecycleAccuracyRules.FindForbiddenPhaseVerbs(prose, SkillLifecyclePhase.Author);

        hits.Should().NotBeEmpty();
        hits.Should().Contain(h => h.Contains("provision", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FindForbiddenPhaseVerbs_SanctionedDeferral_NotFlagged()
    {
        const string prose =
            "This skill generates the Bicep templates. Provisioning happens later, in the azure-deploy skill.";

        var hits = LifecycleAccuracyRules.FindForbiddenPhaseVerbs(prose, SkillLifecyclePhase.Author);

        hits.Should().BeEmpty();
    }

    [Fact]
    public void FindForbiddenPhaseVerbs_ResourceGroupCreation_Flags()
    {
        const string prose =
            "It handles creating resource groups and managed identity setup for the Monitor workspace.";

        var hits = LifecycleAccuracyRules.FindForbiddenPhaseVerbs(prose, SkillLifecyclePhase.Author);

        hits.Should().NotBeEmpty();
    }

    [Fact]
    public void FindForbiddenPhaseVerbs_DeployPhase_NeverFlags()
    {
        const string prose =
            "The Azure Deploy skill provisions the AKS cluster and creates resource groups.";

        var hits = LifecycleAccuracyRules.FindForbiddenPhaseVerbs(prose, SkillLifecyclePhase.Deploy);

        hits.Should().BeEmpty();
    }

    [Fact]
    public void FindForbiddenPhaseVerbs_CleanAuthoringProse_NotFlagged()
    {
        const string prose =
            "This skill generates infrastructure as code and defines the resources in the templates it produces.";

        var hits = LifecycleAccuracyRules.FindForbiddenPhaseVerbs(prose, SkillLifecyclePhase.Author);

        hits.Should().BeEmpty();
    }

    // ---------- #735: FindInternalJargon ----------

    [Theory]
    [InlineData("The pipeline runs a Docker build before publishing.")]
    [InlineData("Then it performs an ACR push to the registry.")]
    [InlineData("Configuration lives in agent.yaml at the repo root.")]
    [InlineData("Messages flow over a duplex WebSocket channel.")]
    [InlineData("Uses invocations_ws for streaming responses.")]
    [InlineData("Supports out-of-band updates to the model.")]
    [InlineData("The training uses SFT and DPO stages.")]
    [InlineData("Applies RFT with grader calibration.")]
    [InlineData("Handles checkpoint selection and dataset versioning.")]
    [InlineData("Pinned to skill-version metadata.")]
    [InlineData("Ships as v1.2 of the skill.")]
    public void FindInternalJargon_JargonToken_Flags(string body)
    {
        var hits = LifecycleAccuracyRules.FindInternalJargon(body);
        hits.Should().NotBeEmpty();
    }

    [Fact]
    public void FindInternalJargon_CleanUserFacingProse_NotFlagged()
    {
        const string body =
            "The Azure Key Vault skill helps you manage secrets, keys, and certificates. " +
            "Ask Copilot to create a vault, set access policies, or rotate a secret.";

        var hits = LifecycleAccuracyRules.FindInternalJargon(body);
        hits.Should().BeEmpty();
    }

    [Fact]
    public void FindInternalJargon_LowercaseAcronymLookalike_NotFlagged()
    {
        // "soft" contains "ft" but not the SFT/DPO/RFT tokens; ensures case-sensitivity.
        const string body = "The soft delete feature protects your Cosmos DB data.";
        var hits = LifecycleAccuracyRules.FindInternalJargon(body);
        hits.Should().BeEmpty();
    }

    [Theory]
    [InlineData("- **PowerShell** (v7.4+)—Install: `winget install Microsoft.PowerShell`")]
    [InlineData("Requires .NET 9.0 or later and Node.js v20.")]
    [InlineData("Install Azure CLI version 2.60 or newer.")]
    public void FindInternalJargon_PrerequisiteRuntimeVersion_NotFlagged(string body)
    {
        // Runtime/tool prerequisite versions are legitimate user-facing content — only the
        // skill's own version string is jargon (#735).
        var hits = LifecycleAccuracyRules.FindInternalJargon(body);
        hits.Should().BeEmpty();
    }

    // ---------- #737: Title fidelity ----------

    [Fact]
    public void CanonicalTitle_BuildsExpectedForm()
    {
        LifecycleAccuracyRules.CanonicalTitle("Azure AI Search")
            .Should().Be("Azure skill for Azure AI Search");
    }

    [Fact]
    public void IsTitleCanonical_ExactMatch_True()
    {
        var ok = LifecycleAccuracyRules.IsTitleCanonical(
            "Azure skill for Azure Monitor", "Azure Monitor", out var expected);

        ok.Should().BeTrue();
        expected.Should().Be("Azure skill for Azure Monitor");
    }

    [Fact]
    public void IsTitleCanonical_Paraphrased_False()
    {
        var ok = LifecycleAccuracyRules.IsTitleCanonical(
            "Monitoring your apps with Azure", "Azure Monitor", out var expected);

        ok.Should().BeFalse();
        expected.Should().Be("Azure skill for Azure Monitor");
    }

    [Fact]
    public void IsTitleCanonical_Null_False()
    {
        var ok = LifecycleAccuracyRules.IsTitleCanonical(null, "Azure Storage", out _);
        ok.Should().BeFalse();
    }
}
