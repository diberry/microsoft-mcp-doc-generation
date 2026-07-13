// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
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

    // ── Bare "Examples" (no colon, no heading) — issue #709 ─────────

    [Fact]
    public void Strip_RemovesBareExamplesBlockWithoutColon()
    {
        // Cosmos DB — bare "Examples" line (no colon, no ###) followed by bullets.
        var content = @"Lists the containers in an Azure Cosmos DB database account.

Examples

- List all containers in database 'inventory'.
- Get throughput for container 'orders' in database 'inventory'.

Example prompts include:

- ""List containers in my Cosmos DB account.""";

        var result = DuplicateExampleStripper.Strip(content);

        Assert.DoesNotContain("List all containers in database", result);
        Assert.DoesNotContain("Get throughput for container", result);
        Assert.Contains("Lists the containers in an Azure Cosmos DB", result);
        Assert.Contains("Example prompts include:", result);
        Assert.Contains("\"List containers in my Cosmos DB account.\"", result);
    }

    [Fact]
    public void Strip_RemovesBareExamplesBlockBeforeParamsComment()
    {
        // Key Vault — exact leaked shape from issue #709: bare "Examples" + bullets
        // sitting directly before the <!-- Required parameters --> annotation comment.
        var content = @"Gets a secret from an Azure Key Vault.

Examples

- Get secret 'db-password' from vault 'contoso-kv'.
- Get the latest version of secret 'api-key' from vault 'prod-kv'.

<!-- Required parameters: 2 - 'Vault', 'Secret' -->

Example prompts include:

- ""Get secret 'db-password' from my key vault.""";

        var result = DuplicateExampleStripper.Strip(content);

        Assert.DoesNotContain("Get secret 'db-password' from vault", result);
        Assert.DoesNotContain("Get the latest version of secret", result);
        // The bare "Examples" header line is gone.
        Assert.DoesNotContain("Examples", result.Split('\n').Select(l => l.Trim()));
        // Structural anchors preserved.
        Assert.Contains("<!-- Required parameters: 2 - 'Vault', 'Secret' -->", result);
        Assert.Contains("Example prompts include:", result);
    }

    [Fact]
    public void Strip_PreservesExamplesWordInProse()
    {
        // "Examples" appears as a word with text following on the same line — legit prose,
        // must NOT be stripped (no false positive).
        var content = @"Examples of supported regions include eastus and westus.

Example prompts include:

- ""List supported regions.""";

        var result = DuplicateExampleStripper.Strip(content);

        Assert.Contains("Examples of supported regions include eastus and westus.", result);
        Assert.Contains("Example prompts include:", result);
    }

    [Fact]
    public void Strip_CanonicalizesBareExamplesBlockWhenCanonicalHeaderIsMissing()
    {
        // Real Step 3 failure shape for monitor instrumentation get-learning-resource:
        // the AI rewrote the canonical "Example prompts include:" heading to bare "Examples".
        var content = @"## Instrumentation: Get learning resource

<!-- @mcpcli monitor instrumentation get-learning-resource -->

Lists all available learning resources for Azure Monitor instrumentation, or retrieves the content of a specific resource by `path`.

Examples

- ""List all learning resource paths for Azure Monitor.""
- ""Get the content for learning resource with path 'docs/instrumentation/aspnetcore'.""

| Parameter |  Required or optional | Description |
|-----------------------|----------------------|-------------|
| **Path** |  Optional | Learning resource path. |";

        var result = DuplicateExampleStripper.Strip(content);

        Assert.Contains("Example prompts include:", result);
        Assert.Contains("List all learning resource paths for Azure Monitor.", result);
        Assert.Contains("docs/instrumentation/aspnetcore", result);
        Assert.DoesNotContain("Examples\n", result);
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
