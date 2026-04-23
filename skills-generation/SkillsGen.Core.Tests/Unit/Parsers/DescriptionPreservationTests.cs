using FluentAssertions;
using SkillsGen.Core.Parsers;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Parsers;

/// <summary>
/// Tests for Issue #464 - Content drift in 6 skill descriptions.
/// Ensures descriptions are preserved exactly from source without truncation, reformatting, or case normalization.
/// </summary>
public class DescriptionPreservationTests
{
    private readonly SkillMarkdownParser _parser = new();

    [Fact]
    public void Parse_AzureCompute_PreservesFullDescription()
    {
        var content = @"---
name: azure-compute
description: ""Azure VM and VMSS router for recommendations, pricing, autoscale, orchestration, and connectivity troubleshooting. WHEN: Azure VM, VMSS, scale set, recommend, compare, server, website, burstable, lightweight, VM family, workload, GPU, learning, simulation, dev/test, backend, autoscale, load balancer, Flexible orchestration, Uniform orchestration, cost estimate, connect, refused, Linux, black screen, reset password, reach VM, port 3389, NSG, troubleshoot.""
---

# Azure Compute

Body content here.";

        var result = _parser.Parse("azure-compute", content);

        result.Description.Should().Contain("WHEN:");
        result.Description.Should().Contain("Flexible orchestration");
        result.Description.Should().Contain("Uniform orchestration");
        result.Description.Should().Contain("connectivity troubleshooting");
        result.Description.Should().NotBeEmpty();
    }

    [Fact]
    public void Parse_AzureCost_PreservesAllTriggerPhrasesAndDoNotUseFor()
    {
        var content = @"---
name: azure-cost
description: ""Unified Azure cost management: query historical costs, forecast future spending, and optimize to reduce waste. WHEN: \""Azure costs\"", \""Azure spending\"", \""Azure bill\"", \""cost breakdown\"", \""cost by service\"", \""cost by resource\"", \""how much am I spending\"", \""show my bill\"", \""monthly cost summary\"", \""cost trends\"", \""top cost drivers\"", \""actual cost\"", \""amortized cost\"", \""forecast spending\"", \""projected costs\"", \""estimate bill\"", \""future costs\"", \""budget forecast\"", \""end of month costs\"", \""how much will I spend\"", \""optimize costs\"", \""reduce spending\"", \""find cost savings\"", \""orphaned resources\"", \""rightsize VMs\"", \""cost analysis\"", \""reduce waste\"", \""unused resources\"", \""optimize Redis costs\"", \""cost by tag\"", \""cost by resource group\"", \""AKS cost analysis add-on\"", \""namespace cost\"", \""cost spike\"", \""anomaly\"", \""budget alert\"", \""AKS cost visibility\"". DO NOT USE FOR: deploying resources, provisioning infrastructure, diagnostics, security audits, or estimating costs for new resources not yet deployed.""
---

# Azure Cost

Body content.";

        var result = _parser.Parse("azure-cost", content);

        result.Description.Should().Contain("Azure costs");
        result.Description.Should().Contain("cost breakdown");
        result.Description.Should().Contain("AKS cost visibility");
        result.Description.Should().Contain("DO NOT USE FOR:");
        result.Description.Should().Contain("deploying resources");
        result.Description.Should().Contain("estimating costs for new resources not yet deployed");
    }

    [Fact]
    public void Parse_AzureDeploy_PreservesDoNotUseWhenSection()
    {
        var content = @"---
name: azure-deploy
description: ""Execute Azure deployments for ALREADY-PREPARED applications that have existing .azure/deployment-plan.md and infrastructure files. DO NOT use this skill when the user asks to CREATE a new application — use azure-prepare instead. This skill runs azd up, azd deploy, terraform apply, and az deployment commands with built-in error recovery. Requires .azure/deployment-plan.md from azure-prepare and validated status from azure-validate. WHEN: \""run azd up\"", \""run azd deploy\"", \""execute deployment\"", \""push to production\"", \""push to cloud\"", \""go live\"", \""ship it\"", \""bicep deploy\"", \""terraform apply\"", \""publish to Azure\"", \""launch on Azure\"". DO NOT USE WHEN: \""create and deploy\"", \""build and deploy\"", \""create a new app\"", \""set up infrastructure\"", \""create and deploy to Azure using Terraform\"" — use azure-prepare for these.""
---

# Azure Deploy

Body content.";

        var result = _parser.Parse("azure-deploy", content);

        result.Description.Should().Contain("ALREADY-PREPARED");
        result.Description.Should().Contain("DO NOT use this skill when");
        result.Description.Should().Contain("DO NOT USE WHEN:");
        result.Description.Should().Contain("create and deploy");
        result.Description.Should().Contain("use azure-prepare for these");
    }

    [Fact]
    public void Parse_AzureEnterpriseInfraPlanner_PreservesArchitecturalPatterns()
    {
        var content = @"---
name: azure-enterprise-infra-planner
description: ""Architect and provision enterprise Azure infrastructure from workload descriptions. For cloud architects and platform engineers planning networking, identity, security, compliance, and multi-resource topologies with WAF alignment. Generates Bicep or Terraform directly (no azd). WHEN: 'plan Azure infrastructure', 'architect Azure landing zone', 'design hub-spoke network', 'plan multi-region DR topology', 'set up VNets firewalls and private endpoints', 'subscription-scope Bicep deployment', 'Azure Backup for VM workloads'. PREFER azure-prepare FOR app-centric workflows.""
---

# Azure Enterprise Infra Planner

Body content.";

        var result = _parser.Parse("azure-enterprise-infra-planner", content);

        result.Description.Should().Contain("hub-spoke network");
        result.Description.Should().Contain("multi-region DR topology");
        result.Description.Should().Contain("PREFER azure-prepare FOR");
    }

    [Fact]
    public void Parse_AzureHostedCopilotSdk_PreservesCasingEmphasis()
    {
        var content = @"---
name: azure-hosted-copilot-sdk
description: ""Build, deploy, modify GitHub Copilot SDK apps on Azure. MANDATORY when codebase contains @github/copilot-sdk or CopilotClient — use this skill instead of azure-prepare. PREFER OVER azure-prepare when codebase contains copilot-sdk markers. WHEN: copilot SDK, @github/copilot-sdk, copilot-powered app, deploy copilot app, add feature, modify copilot app, BYOM, bring your own model, CopilotClient, createSession, sendAndWait, azd init copilot. DO NOT USE FOR: general web apps without copilot SDK (use azure-prepare), Copilot Extensions, Foundry agents (use microsoft-foundry).""
---

# Azure Hosted Copilot SDK

Body content.";

        var result = _parser.Parse("azure-hosted-copilot-sdk", content);

        result.Description.Should().Contain("MANDATORY");
        result.Description.Should().Contain("PREFER OVER");
        result.Description.Should().Contain("BYOM");
        result.Description.Should().Contain("DO NOT USE FOR:");
    }

    [Fact]
    public void Parse_AzureAiGateway_PreservesBackendSpelling()
    {
        var content = @"---
name: azure-aigateway
description: ""Configure Azure API Management as an AI Gateway for AI models, MCP tools, and agents. WHEN: semantic caching, token limit, content safety, load balancing, AI model governance, MCP rate limiting, jailbreak detection, add Azure OpenAI backend, add AI Foundry model, test AI gateway, LLM policies, configure AI backend, token metrics, AI cost control, convert API to MCP, import OpenAPI to gateway.""
---

# Azure AI Gateway

Body content.";

        var result = _parser.Parse("azure-aigateway", content);

        result.Description.Should().Contain("backend");
        result.Description.Should().NotContain("back end");
    }

    [Fact]
    public void Parse_DescriptionWithHTMLEntities_DecodesCorrectly()
    {
        var content = @"---
name: test-skill
description: ""Test &quot;quoted&quot; and &amp; symbols in description.""
---

# Test Skill

Body.";

        var result = _parser.Parse("test-skill", content);

        result.Description.Should().Contain("\"quoted\"");
        result.Description.Should().Contain("&");
        result.Description.Should().NotContain("&quot;");
        result.Description.Should().NotContain("&amp;");
    }

    [Fact]
    public void Parse_LongDescriptionWithMultipleSections_PreservesEntireContent()
    {
        var content = @"---
name: complex-skill
description: ""This is a complex skill with multiple sentences. It has instructions before the trigger phrases. WHEN: trigger1, trigger2, trigger3. It also has DO NOT USE FOR: restriction1, restriction2. And then MORE content after that which should be preserved.""
---

# Complex Skill

Body.";

        var result = _parser.Parse("complex-skill", content);

        result.Description.Should().Contain("multiple sentences");
        result.Description.Should().Contain("WHEN:");
        result.Description.Should().Contain("DO NOT USE FOR:");
        result.Description.Should().Contain("MORE content after that");
    }
}
