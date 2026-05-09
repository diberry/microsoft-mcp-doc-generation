// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

public class CliSectionInjectorTests
{
    private static Dictionary<string, CliCommand> CreateTestCliCommands()
    {
        return new Dictionary<string, CliCommand>(StringComparer.OrdinalIgnoreCase)
        {
            ["storage account create"] = new CliCommand
            {
                Command = "storage account create",
                Description = "Creates an Azure Storage account in the specified resource group.",
                Option = new List<CliOption>
                {
                    new() { Name = "--tenant", Description = "Tenant ID", Type = "string" },
                    new() { Name = "--subscription", Description = "Subscription", Type = "string" },
                    new() { Name = "--retry-delay", Description = "Retry delay", Type = "string" },
                    new() { Name = "--retry-max-delay", Description = "Max retry delay", Type = "string" },
                    new() { Name = "--retry-max-retries", Description = "Max retries", Type = "string" },
                    new() { Name = "--retry-mode", Description = "Retry mode", Type = "string" },
                    new() { Name = "--retry-network-timeout", Description = "Network timeout", Type = "string" },
                    new() { Name = "--auth-method", Description = "Auth method", Type = "string" },
                    new() { Name = "--learn", Description = "Learn mode", Type = "string" },
                    new() { Name = "--account", Description = "Storage account name.", Type = "string", Required = true },
                    new() { Name = "--resource-group", Description = "Resource group name.", Type = "string", Required = true },
                    new() { Name = "--location", Description = "Azure region.", Type = "string", Required = true },
                    new() { Name = "--sku", Description = "Storage SKU.", Type = "string" },
                    new() { Name = "--access-tier", Description = "Access tier.", Type = "string" },
                },
                Enrichment = new CliEnrichment
                {
                    ParameterEnhancements = new Dictionary<string, CliParameterEnhancement>
                    {
                        ["--account"] = new() { ValuePlaceholder = "unique-account-name" },
                        ["--resource-group"] = new() { ValuePlaceholder = "resource-group" },
                        ["--location"] = new() { ValuePlaceholder = "location" },
                    }
                }
            },
            ["storage account get"] = new CliCommand
            {
                Command = "storage account get",
                Description = "Retrieves storage account details.",
                Option = new List<CliOption>
                {
                    new() { Name = "--tenant", Description = "Tenant", Type = "string" },
                    new() { Name = "--subscription", Description = "Sub", Type = "string" },
                    new() { Name = "--account", Description = "Account name.", Type = "string" },
                    new() { Name = "--learn", Description = "Learn", Type = "string" },
                }
            }
        };
    }

    [Fact]
    public void InjectCliSection_InsertsAfterAnnotationLine()
    {
        var content = @"## Account: create

<!-- @mcpcli storage account create -->

Description here.

| Parameter | Required or optional | Description |
|-----------|---------------------|-------------|
| **Account** | Required | Account name. |

[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):

Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌";

        var result = CliSectionInjector.InjectCliSection(content, "storage account create", CreateTestCliCommands());

        Assert.Contains("#### CLI", result);
        Assert.Contains("Creates an Azure Storage account", result);
        Assert.Contains("```bash", result);
        Assert.Contains("azmcp storage account create", result);
        Assert.Contains("--account <unique-account-name>", result);
        Assert.Contains("--resource-group <resource-group>", result);
        // CLI section should come after the annotation line
        var annotationIndex = result.IndexOf("Local Required: ❌");
        var cliIndex = result.IndexOf("#### CLI");
        Assert.True(cliIndex > annotationIndex, "CLI section should appear after annotation hints");
    }

    [Fact]
    public void InjectCliSection_ExcludesCommonParams()
    {
        var content = "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌";
        var result = CliSectionInjector.InjectCliSection(content, "storage account create", CreateTestCliCommands());

        // Common params should NOT appear in the CLI table
        Assert.DoesNotContain("| `--tenant`", result);
        Assert.DoesNotContain("| `--subscription`", result);
        Assert.DoesNotContain("| `--retry-delay`", result);
        Assert.DoesNotContain("| `--retry-max-delay`", result);
        Assert.DoesNotContain("| `--retry-max-retries`", result);
        Assert.DoesNotContain("| `--retry-mode`", result);
        Assert.DoesNotContain("| `--retry-network-timeout`", result);
        Assert.DoesNotContain("| `--auth-method`", result);
        Assert.DoesNotContain("| `--learn`", result);

        // Domain params SHOULD appear
        Assert.Contains("| `--account`", result);
        Assert.Contains("| `--resource-group`", result);
        Assert.Contains("| `--location`", result);
        Assert.Contains("| `--sku`", result);
    }

    [Fact]
    public void InjectCliSection_RequiredParamsUseBraces_OptionalUseBrackets()
    {
        var content = "Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌";
        var result = CliSectionInjector.InjectCliSection(content, "storage account create", CreateTestCliCommands());

        // Required params: --param <value> (no brackets)
        Assert.Contains("--account <unique-account-name>", result);
        // Optional params: [--param <value>]
        Assert.Contains("[--sku <sku>]", result);
        Assert.Contains("[--access-tier <access-tier>]", result);
    }

    [Fact]
    public void InjectCliSection_NoMatchingCommand_ReturnsOriginal()
    {
        var content = "Some tool content";
        var result = CliSectionInjector.InjectCliSection(content, "nonexistent command", CreateTestCliCommands());
        Assert.Equal(content, result);
    }

    [Fact]
    public void InjectCliSection_NullCommand_ReturnsOriginal()
    {
        var content = "Some tool content";
        var result = CliSectionInjector.InjectCliSection(content, null, CreateTestCliCommands());
        Assert.Equal(content, result);
    }

    [Fact]
    public void InjectCliSection_EmptyCliCommands_ReturnsOriginal()
    {
        var content = "Some tool content";
        var result = CliSectionInjector.InjectCliSection(content, "storage account create", new Dictionary<string, CliCommand>());
        Assert.Equal(content, result);
    }

    [Fact]
    public void InjectCliSection_AllOptionalParams_UsesBrackets()
    {
        var content = "Destructive: ❌ | Idempotent: ✅ | Open World: ❌ | Read Only: ✅ | Secret: ❌ | Local Required: ❌";
        var result = CliSectionInjector.InjectCliSection(content, "storage account get", CreateTestCliCommands());

        // account is optional in this command
        Assert.Contains("[--account <account>]", result);
        Assert.Contains("| `--account` | ❌ |", result);
    }

    [Fact]
    public void InjectCliSection_RequiredParamsMarkedCorrectly()
    {
        var content = "Destructive: ✅ | Idempotent: ❌ | Open World: ❌ | Read Only: ❌ | Secret: ❌ | Local Required: ❌";
        var result = CliSectionInjector.InjectCliSection(content, "storage account create", CreateTestCliCommands());

        Assert.Contains("| `--account` | ✅ |", result);
        Assert.Contains("| `--resource-group` | ✅ |", result);
        Assert.Contains("| `--location` | ✅ |", result);
        Assert.Contains("| `--sku` | ❌ |", result);
    }

    [Fact]
    public void BuildCliSection_ContainsExpectedStructure()
    {
        var cmd = CreateTestCliCommands()["storage account create"];
        var section = CliSectionInjector.BuildCliSection("storage account create", cmd);

        Assert.StartsWith("#### CLI", section);
        Assert.Contains("```bash", section);
        Assert.Contains("```", section);
        Assert.Contains("| Switch | Required | Type | Description |", section);
        Assert.Contains("|--------|----------|------|-------------|", section);
    }

    [Fact]
    public void BuildCliSection_MultilineDescription_CollapsedToSingleLine()
    {
        var cmd = new CliCommand
        {
            Command = "test cmd",
            Description = "Line one\nLine two\r\nLine three",
            Option = new List<CliOption>()
        };

        var section = CliSectionInjector.BuildCliSection("test cmd", cmd);
        Assert.Contains("Line one Line two Line three", section);
        Assert.DoesNotContain("\nLine two", section);
    }
}
