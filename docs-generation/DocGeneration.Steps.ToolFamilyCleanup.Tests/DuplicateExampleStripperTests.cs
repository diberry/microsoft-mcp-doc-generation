// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for DuplicateExampleStripper to ensure non-canonical example blocks
/// are removed while preserving the "Example prompts include:" canonical format.
/// Fixes: #153 — AI generates duplicate Examples block.
/// </summary>
public class DuplicateExampleStripperTests
{
    // ── Core stripping behavior ─────────────────────────────────────

    [Fact]
    public void Strip_RemovesBlockquoteExampleLine()
    {
        var content = @"Description of the tool.

> Example: Get web app 'my-webapp' in resource group 'my-rg'

<!-- Required parameters: 0 -  -->

Example prompts include:

- ""List all web apps.""";

        var result = DuplicateExampleStripper.Strip(content);

        Assert.DoesNotContain("> Example: Get web app", result);
        Assert.Contains("Example prompts include:", result);
        Assert.Contains("\"List all web apps.\"", result);
    }

    [Fact]
    public void Strip_RemovesRawExamplesBulletList()
    {
        var content = @"Description of the tool.

Examples:
- Get details for a specific web app: ""Get web app 'my-webapp'""
- List all web apps: ""List all web apps in the subscription""

<!-- Required parameters: 0 -  -->

Example prompts include:

- ""List all web apps in my subscription.""";

        var result = DuplicateExampleStripper.Strip(content);

        Assert.DoesNotContain("Examples:", result);
        Assert.DoesNotContain("Get details for a specific web app", result);
        Assert.Contains("Example prompts include:", result);
        Assert.Contains("\"List all web apps in my subscription.\"", result);
    }

    [Fact]
    public void Strip_RemovesExamplePromptsWithoutInclude()
    {
        var content = @"[Tool annotation hints](index.md#tool-annotations):
Destructive: no

Example prompts:
- ""Diagnose web app 'finance-backend'.""
- ""Diagnose web app 'orders-api'.""

<!-- Required parameters: 2 -->

Example prompts include:

- ""Run diagnostics on web app 'my-app'.""";

        var result = DuplicateExampleStripper.Strip(content);

        // "Example prompts:" (without "include") should be removed
        Assert.DoesNotContain("Example prompts:\n", result);
        Assert.DoesNotContain("finance-backend", result);
        // Canonical should remain
        Assert.Contains("Example prompts include:", result);
        Assert.Contains("Run diagnostics on web app", result);
    }

    [Fact]
    public void Strip_PreservesCanonicalExamplePrompts()
    {
        var content = @"Description of the tool.

Example prompts include:

- ""Create a storage account named 'mystorageaccount'.""
- ""List all storage accounts in resource group 'rg-prod'.""

| Parameter | Required or optional | Description |";

        var result = DuplicateExampleStripper.Strip(content);

        Assert.Contains("Example prompts include:", result);
        Assert.Contains("Create a storage account", result);
        Assert.Contains("List all storage accounts", result);
    }

    [Fact]
    public void Strip_HandlesMultipleDuplicateFormats()
    {
        var content = @"Description.

> Example: add database 'mydb' to app 'my-webapp'

Examples:
- Add a database: ""Add database 'mydb'""
- Remove a database: ""Remove database 'mydb'""

Example prompts include:

- ""Add database 'ordersdb' to app 'order-api'.""";

        var result = DuplicateExampleStripper.Strip(content);

        Assert.DoesNotContain("> Example:", result);
        Assert.DoesNotContain("Examples:", result);
        Assert.Contains("Example prompts include:", result);
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Strip_NoDuplicates_ReturnsUnchanged()
    {
        var content = @"Description.

Example prompts include:

- ""List all accounts.""

| Parameter | Required | Description |";

        var result = DuplicateExampleStripper.Strip(content);

        Assert.Equal(content, result);
    }

    [Fact]
    public void Strip_NullOrEmpty_ReturnsInput()
    {
        Assert.Equal("", DuplicateExampleStripper.Strip(""));
        Assert.Null(DuplicateExampleStripper.Strip(null!));
    }

    [Fact]
    public void Strip_PreservesNonExampleBlockquotes()
    {
        var content = @"> [!NOTE]
> This is an important note.

Example prompts include:

- ""List accounts.""";

        var result = DuplicateExampleStripper.Strip(content);

        // Non-example blockquotes should be preserved
        Assert.Contains("> [!NOTE]", result);
        Assert.Contains("> This is an important note.", result);
    }

    [Fact]
    public void Strip_PreservesExamplesHeadingInOtherContexts()
    {
        // "## Examples" as a real H2 heading should NOT be removed
        // (that's handled by phantom H2 stripping, not this)
        var content = @"## Examples

Some example text.

Example prompts include:

- ""List items.""";

        var result = DuplicateExampleStripper.Strip(content);

        // H2 headings are not our responsibility
        Assert.Contains("## Examples", result);
    }
}
