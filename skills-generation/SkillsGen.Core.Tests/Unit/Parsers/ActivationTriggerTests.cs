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
        // Detection markers should come from description, not body code samples
        var description = "Build, deploy, modify GitHub Copilot SDK apps on Azure. MANDATORY when codebase contains @github/copilot-sdk. Detects `CopilotClient` and `createSession` in your project files.";
        var body = "## Usage\nSome body content with `random` backtick code.";

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
    public void ExtractActivationTriggers_CodebaseContainsPattern_InDescription_Extracted()
    {
        // Activation markers must be in description, not body
        var description = "This skill is MANDATORY when codebase contains `@microsoft/teams-ai` library.";
        var body = "";

        var result = SkillMarkdownParser.ExtractActivationTriggers(description, body);

        result.Should().NotBeNull();
        result!.DetectionMarkers.Should().Contain("@microsoft/teams-ai");
    }

    [Fact]
    public void ExtractActivationTriggers_BodyOnly_ReturnsNull()
    {
        // Body-only content should NOT produce activation triggers (#497)
        var description = "";
        var body = "This skill is MANDATORY when codebase contains `@microsoft/teams-ai` library.";

        var result = SkillMarkdownParser.ExtractActivationTriggers(description, body);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractActivationTriggers_DoesNotGrabBodyNotes_AsDirective()
    {
        // Regression test for #497: "Mandatory." in a body note was being extracted as MANDATORY directive
        var description = "Provision Microsoft Entra Agent Identity Blueprints. USE FOR: Agent Identity Blueprint, BlueprintPrincipal.";
        var body = @"## Step 2: Create BlueprintPrincipal

> Mandatory. Creating a Blueprint does NOT auto-create its service principal.

```python
headers = {'Authorization': 'Bearer token'}
```";

        var result = SkillMarkdownParser.ExtractActivationTriggers(description, body);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractActivationTriggers_DoesNotGrabPythonVariables_AsMarkers()
    {
        // Regression test for #497: `headers` variable from code samples extracted as detection marker
        var description = "Manage Entra Agent IDs. USE FOR: agent identity, token exchange.";
        var body = @"### Python (application)

```python
headers = {'Authorization': f'Bearer {token.token}', 'Content-Type': 'application/json'}
```

Use the `requests` client and `headers` dict from above.";

        var result = SkillMarkdownParser.ExtractActivationTriggers(description, body);

        result.Should().BeNull();
    }
}
