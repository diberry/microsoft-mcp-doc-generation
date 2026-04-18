using FluentAssertions;
using SkillsGen.Core.Models;
using SkillsGen.Core.Parsers;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Parsers;

public class ActivationTriggerTests
{
    [Fact]
    public void ExtractActivationTriggers_MandatoryDirective_Parsed()
    {
        var description = "MANDATORY when codebase contains @github/copilot-sdk or CopilotClient — use this skill instead of azure-prepare.";
        var body = "";

        var result = SkillMarkdownParser.ExtractActivationTriggers(description, body);

        result.Should().NotBeNull();
        result!.Directive.Should().Contain("MANDATORY");
    }

    [Fact]
    public void ExtractActivationTriggers_PreferOverDirective_Parsed()
    {
        var description = "PREFER OVER azure-prepare when codebase contains copilot-sdk markers.";
        var body = "";

        var result = SkillMarkdownParser.ExtractActivationTriggers(description, body);

        result.Should().NotBeNull();
        result!.PreferOver.Should().Be("azure-prepare");
        result.Directive.Should().Contain("PREFER OVER");
    }

    [Fact]
    public void ExtractActivationTriggers_NpmPackageMarkers_Extracted()
    {
        var description = "Build, deploy, modify GitHub Copilot SDK apps on Azure. MANDATORY when codebase contains @github/copilot-sdk.";
        var body = "Detects `CopilotClient` and `createSession` in your project files.";

        var result = SkillMarkdownParser.ExtractActivationTriggers(description, body);

        result.Should().NotBeNull();
        result!.DetectionMarkers.Should().Contain("@github/copilot-sdk");
        result.DetectionMarkers.Should().Contain("CopilotClient");
        result.DetectionMarkers.Should().Contain("createSession");
    }

    [Fact]
    public void ExtractActivationTriggers_NoDirectives_ReturnsNull()
    {
        var description = "Manage Azure Storage resources including blob containers and file shares.";
        var body = "## Services\n\n- Azure Blob Storage\n- Azure File Shares";

        var result = SkillMarkdownParser.ExtractActivationTriggers(description, body);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractActivationTriggers_EmptyInputs_ReturnsNull()
    {
        var result = SkillMarkdownParser.ExtractActivationTriggers("", "");
        result.Should().BeNull();
    }

    [Fact]
    public void ExtractActivationTriggers_CodebaseContainsPattern_Extracted()
    {
        var description = "";
        var body = "This skill is MANDATORY when codebase contains `@microsoft/teams-ai` library.";

        var result = SkillMarkdownParser.ExtractActivationTriggers(description, body);

        result.Should().NotBeNull();
        result!.DetectionMarkers.Should().Contain("@microsoft/teams-ai");
    }
}
