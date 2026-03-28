// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for EngineeringExampleStripper to ensure engineering-authored example
/// patterns from MCP CLI source descriptions are stripped from tool-family articles.
/// Fixes: #278 — Engineering description H3 Examples and inline examples not stripped.
/// </summary>
public class EngineeringExampleStripperTests
{
    // ── Pattern 1: H3 ### Examples block ────────────────────────────

    [Fact]
    public void Strip_RemovesH3ExamplesBlockWithBulletItems()
    {
        var content = @"This tool lists Azure Virtual Machine Scale Sets (VMSS) and their instances. This tool is part of the Model Context Protocol (MCP) server.

### Examples

- Get the VMSS named 'web-scale' in resource group 'rg-prod'
- Get instance '2' of VMSS 'web-scale' in resource group 'rg-prod'
- List VMSS in subscription '01234567-89ab-cdef-0123-456789abcdef'

Example prompts include:

- ""List all virtual machine scale sets in my subscription.""";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.DoesNotContain("### Examples", result);
        Assert.DoesNotContain("Get the VMSS named", result);
        Assert.DoesNotContain("Get instance '2'", result);
        Assert.Contains("Example prompts include:", result);
        Assert.Contains("\"List all virtual machine scale sets in my subscription.\"", result);
    }

    [Fact]
    public void Strip_RemovesH3ExampleSingularBlockWithBulletItems()
    {
        var content = @"Description text.

### Example

- Create a storage account named 'myaccount'

Example prompts include:

- ""Create a new storage account.""";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.DoesNotContain("### Example", result);
        Assert.DoesNotContain("Create a storage account named", result);
        Assert.Contains("Example prompts include:", result);
    }

    // ── Pattern 2: Standalone Example: line ─────────────────────────

    [Fact]
    public void Strip_RemovesStandaloneExampleLine()
    {
        var content = @"This tool deletes an Azure Virtual Machine (VM). The operation is irreversible.

Example: Delete VM 'web-prod-01' in resource group 'rg-prod'.

Example prompts include:

- ""Delete virtual machine 'my-vm'.""";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.DoesNotContain("Example: Delete VM", result);
        Assert.Contains("This tool deletes", result);
        Assert.Contains("Example prompts include:", result);
    }

    [Fact]
    public void Strip_RemovesStandaloneExampleWithQuotedText()
    {
        var content = @"Annotation values.

Example: ""Generate a GitHub Actions pipeline that provisions infrastructure and deploys a Node.js app to Azure App Service.""

## Get deploy plan";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.DoesNotContain("Example: \"Generate a GitHub Actions", result);
        Assert.Contains("## Get deploy plan", result);
    }

    [Fact]
    public void Strip_RemovesStandaloneExampleWithSingleQuotedText()
    {
        var content = @"Description text.

Example: 'Recommend a file share in eastus with 512 GiB'

Example prompts include:";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.DoesNotContain("Example: 'Recommend", result);
        Assert.Contains("Example prompts include:", result);
    }

    // ── Pattern 3: Inline Example prompt: in paragraph ──────────────

    [Fact]
    public void Strip_RemovesInlineExamplePromptAtEndOfParagraph()
    {
        var content = @"This tool creates an Azure Virtual Machine Scale Set (VMSS) to run multiple identical virtual machine instances. The tool accepts existing virtual network and subnet names, or it creates a new virtual network if none is provided. Example prompt: Create a VMSS named 'web-scale' in resource group 'prod-rg' in location 'eastus' with 3 instances and `Standard_D2s_v3` VM size.

Example prompts include:

- ""Create a scale set with 5 instances.""";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.DoesNotContain("Example prompt:", result);
        Assert.DoesNotContain("Create a VMSS named 'web-scale'", result);
        Assert.Contains("it creates a new virtual network if none is provided.", result);
        Assert.Contains("Example prompts include:", result);
    }

    [Fact]
    public void Strip_RemovesStandaloneExamplePromptLine()
    {
        var content = @"Description of the tool.

Example prompt: List detectors for web app 'my-webapp' in resource group 'my-rg'.

Example prompts include:

- ""List all detectors.""";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.DoesNotContain("Example prompt: List detectors", result);
        Assert.Contains("Description of the tool.", result);
        Assert.Contains("Example prompts include:", result);
    }

    [Fact]
    public void Strip_RemovesStandaloneExamplePromptWithQuotedText()
    {
        var content = @"Description of the tool.

Example prompt: ""List resource groups in subscription 'Contoso-Prod-Sub'""

Example prompts include:

- ""List all resource groups.""";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.DoesNotContain("Example prompt:", result);
        Assert.DoesNotContain("Contoso-Prod-Sub", result);
        Assert.Contains("Example prompts include:", result);
    }

    [Fact]
    public void Strip_RemovesMultiLineExamplePromptBlock()
    {
        // deploy.md pattern: "Example prompt:\n"text""
        var content = @"Common resource types include Azure App Service and Azure Container Apps.

Example prompt:
""Get IaC rules for 'appservice' and 'azuresqldatabase' using 'AzCli'""

<!-- Required parameters: 1 -->

Example prompts include:";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.DoesNotContain("Example prompt:", result);
        Assert.DoesNotContain("Get IaC rules", result);
        Assert.Contains("Common resource types", result);
        Assert.Contains("<!-- Required parameters: 1 -->", result);
    }

    // ── Preservation tests (no false positives) ─────────────────────

    [Fact]
    public void Strip_PreservesCanonicalExamplePromptsInclude()
    {
        var content = @"Description.

Example prompts include:

- ""Create a storage account named 'mystorageaccount'.""
- ""List all storage accounts in resource group 'rg-prod'.""

| Parameter | Required or optional | Description |";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.Contains("Example prompts include:", result);
        Assert.Contains("Create a storage account", result);
        Assert.Contains("List all storage accounts", result);
    }

    [Fact]
    public void Strip_PreservesH2ExamplesHeading()
    {
        // H2 "## Examples" is handled by phantom H2 stripping, not this
        var content = @"## Examples

Some example text.

Example prompts include:

- ""List items.""";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.Contains("## Examples", result);
    }

    [Fact]
    public void Strip_PreservesBlockquoteExamples()
    {
        // Blockquote examples are handled by DuplicateExampleStripper
        var content = @"> Example: Get web app 'my-webapp'

Example prompts include:

- ""List web apps.""";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.Contains("> Example:", result);
    }

    [Fact]
    public void Strip_PreservesNonExampleH3Headings()
    {
        var content = @"### Prerequisites

You need an Azure subscription.

Example prompts include:

- ""List all resources.""";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.Contains("### Prerequisites", result);
        Assert.Contains("You need an Azure subscription.", result);
    }

    [Fact]
    public void Strip_PreservesExampleWordInNormalText()
    {
        var content = @"For example, you can create a storage account. This is an example of good practice.

Example prompts include:

- ""Create a storage account.""";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.Contains("For example, you can create", result);
        Assert.Contains("This is an example of good practice.", result);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Strip_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", EngineeringExampleStripper.Strip(""));
        Assert.Null(EngineeringExampleStripper.Strip(null!));
    }

    [Fact]
    public void Strip_NoDuplicates_ReturnsUnchanged()
    {
        var content = @"Description.

Example prompts include:

- ""List all accounts.""

| Parameter | Required | Description |";

        var result = EngineeringExampleStripper.Strip(content);

        Assert.Equal(content, result);
    }

    [Fact]
    public void Strip_HandlesMultiplePatternsInOneDocument()
    {
        var content = @"## Create virtual machine scale set

<!-- @mcpcli compute vmss create -->

This tool creates a VMSS. Example prompt: Create a VMSS named 'web-scale' in resource group 'prod-rg'.

Example prompts include:

- ""Create a scale set.""

## Delete virtual machine

<!-- @mcpcli compute vm delete -->

This tool deletes a VM.

Example: Delete VM 'web-prod-01' in resource group 'rg-prod'.

Example prompts include:

- ""Delete my VM.""

## Get virtual machine scale set

<!-- @mcpcli compute vmss get -->

This tool lists VMSS instances.

### Examples

- Get the VMSS named 'web-scale'
- List VMSS in subscription '012345'

Example prompts include:

- ""List all scale sets.""";

        var result = EngineeringExampleStripper.Strip(content);

        // All three engineering patterns should be removed
        Assert.DoesNotContain("Example prompt: Create a VMSS", result);
        Assert.DoesNotContain("Example: Delete VM", result);
        Assert.DoesNotContain("### Examples", result);
        Assert.DoesNotContain("Get the VMSS named", result);

        // Canonical prompts and structure preserved
        Assert.Equal(3, CountOccurrences(result, "Example prompts include:"));
        Assert.Contains("## Create virtual machine scale set", result);
        Assert.Contains("## Delete virtual machine", result);
        Assert.Contains("## Get virtual machine scale set", result);
    }

    [Fact]
    public void Strip_CleansUpExcessiveBlankLines()
    {
        var content = "Description.\n\nExample: Delete VM 'test'.\n\nExample prompts include:";

        var result = EngineeringExampleStripper.Strip(content);

        // Should not have 3+ consecutive newlines
        Assert.DoesNotContain("\n\n\n", result);
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, StringComparison.Ordinal)) != -1)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }
}
