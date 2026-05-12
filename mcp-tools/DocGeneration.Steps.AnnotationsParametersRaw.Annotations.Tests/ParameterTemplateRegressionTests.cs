// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using TemplateEngine;
using Xunit;

namespace DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests;

/// <summary>
/// Regression tests for raw CLI parameter name rendering in templates.
/// These tests verify that templates render parameter names in their raw CLI form
/// (backtick-wrapped, e.g., **`--account-name`**) and do NOT render NL-converted
/// names (e.g., "Account name").
///
/// If someone reverts the templates to NL name rendering, these tests FAIL.
/// </summary>
public class ParameterTemplateRegressionTests
{
    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    // ── parameter-template.hbs ──────────────────────────────────────────

    [Fact]
    public async Task ParameterTemplate_RendersRawCliName_InBackticks()
    {
        var templateContent = await LoadTemplateAsync("parameter-template.hbs");
        var data = new Dictionary<string, object>
        {
            ["generateParameter"] = true,
            ["option"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["name"] = "--account-name",
                    ["RequiredText"] = "Required",
                    ["description"] = "The name of the storage account."
                },
                new Dictionary<string, object>
                {
                    ["name"] = "--resource-group",
                    ["RequiredText"] = "Optional",
                    ["description"] = "The resource group containing the account."
                }
            },
            ["hasConditionalRequired"] = false
        };

        var result = Normalize(HandlebarsTemplateEngine.ProcessTemplateString(templateContent, data));

        // Raw CLI names must appear backtick-wrapped
        Assert.Contains("**`--account-name`**", result);
        Assert.Contains("**`--resource-group`**", result);

        // NL-converted names must NOT appear
        Assert.DoesNotContain("Account name", result);
        Assert.DoesNotContain("Resource group", result);
    }

    [Fact]
    public async Task ParameterTemplate_ConditionalRequired_ShowsAsteriskNote()
    {
        var templateContent = await LoadTemplateAsync("parameter-template.hbs");
        var data = new Dictionary<string, object>
        {
            ["generateParameter"] = true,
            ["option"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["name"] = "--subscription-id",
                    ["RequiredText"] = "Required*",
                    ["description"] = "The Azure subscription ID."
                }
            },
            ["hasConditionalRequired"] = true
        };

        var result = Normalize(HandlebarsTemplateEngine.ProcessTemplateString(templateContent, data));

        Assert.Contains("**`--subscription-id`**", result);
        Assert.Contains("Required*", result);
        Assert.Contains("At least one of the parameters marked with * is required.", result);
    }

    // ── area-template.hbs ───────────────────────────────────────────────

    [Fact]
    public async Task AreaTemplate_RendersRawCliParamNames_NotNlNames()
    {
        var templateContent = await LoadTemplateAsync("area-template.hbs");
        var data = new Dictionary<string, object>
        {
            ["generateAreaPage"] = true,
            ["areaName"] = "Azure Cosmos DB",
            ["areaData"] = new Dictionary<string, object>
            {
                ["description"] = "Tools for managing Azure Cosmos DB resources.",
                ["toolCount"] = 1
            },
            ["tools"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["command"] = "cosmos_db account create",
                    ["description"] = "Creates a Cosmos DB account.",
                    ["option"] = new[]
                    {
                        new Dictionary<string, object>
                        {
                            ["name"] = "--account-name",
                            ["RequiredText"] = "Required",
                            ["description"] = "The Cosmos DB account name."
                        },
                        new Dictionary<string, object>
                        {
                            ["name"] = "--default-consistency-level",
                            ["RequiredText"] = "Optional*",
                            ["description"] = "The default consistency level."
                        }
                    },
                    ["annotationContent"] = "",
                    ["annotationFileName"] = ""
                }
            },
            ["version"] = "1.0.0-test",
            ["generatedAt"] = "2026-05-12T00:00:00Z"
        };

        var result = Normalize(HandlebarsTemplateEngine.ProcessTemplateString(templateContent, data));

        // Raw CLI parameter names in backticks
        Assert.Contains("**`--account-name`**", result);
        Assert.Contains("**`--default-consistency-level`**", result);

        // NL-converted forms must not appear
        Assert.DoesNotContain("Account name", result);
        Assert.DoesNotContain("Default consistency level", result);

        // Required/Optional markers present
        Assert.Contains("Required", result);
        Assert.Contains("Optional*", result);
    }

    // ── param-annotation-template.hbs ───────────────────────────────────

    [Fact]
    public async Task ParamAnnotationTemplate_RendersRawCliParamNames()
    {
        var templateContent = await LoadTemplateAsync("param-annotation-template.hbs");
        var data = new Dictionary<string, object>
        {
            ["generateParameterAndAnnotation"] = true,
            ["option"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["name"] = "--location",
                    ["RequiredText"] = "Required",
                    ["description"] = "The Azure region for the resource."
                },
                new Dictionary<string, object>
                {
                    ["name"] = "--sku",
                    ["RequiredText"] = "Optional",
                    ["description"] = "The SKU tier for the service."
                }
            },
            ["metadata"] = new Dictionary<string, object>
            {
                ["destructive"] = new Dictionary<string, object>
                {
                    ["name"] = "Destructive",
                    ["value"] = false,
                    ["description"] = "Whether this tool is destructive."
                },
                ["readOnly"] = new Dictionary<string, object>
                {
                    ["name"] = "ReadOnly",
                    ["value"] = true,
                    ["description"] = "Whether this tool is read-only."
                }
            }
        };

        var result = Normalize(HandlebarsTemplateEngine.ProcessTemplateString(templateContent, data));

        // Raw CLI names in backticks
        Assert.Contains("**`--location`**", result);
        Assert.Contains("**`--sku`**", result);

        // NL-converted names must not appear as standalone parameter names
        // (check that the table cells don't have bare "Location" or "Sku" as the param name)
        Assert.DoesNotContain("| **`Location`**", result);
        Assert.DoesNotContain("| **`Sku`**", result);
        // Metadata renders correctly
        Assert.Contains("Destructive: ❌", result);
        Assert.Contains("ReadOnly: ✅", result);
    }

    // ── common-tools.hbs ────────────────────────────────────────────────

    [Fact]
    public async Task CommonToolsTemplate_RendersPascalCaseName_InBackticks()
    {
        var templateContent = await LoadTemplateAsync("common-tools.hbs");
        var data = new Dictionary<string, object>
        {
            ["commonParameters"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["Name"] = "SubscriptionId",
                    ["Type"] = "string",
                    ["IsRequired"] = true,
                    ["Description"] = "The Azure subscription identifier.",
                    ["Source"] = "Authentication"
                },
                new Dictionary<string, object>
                {
                    ["Name"] = "ResourceGroupName",
                    ["Type"] = "string",
                    ["IsRequired"] = false,
                    ["Description"] = "The name of the resource group.",
                    ["Source"] = "Authentication"
                },
                new Dictionary<string, object>
                {
                    ["Name"] = "TenantId",
                    ["Type"] = "string",
                    ["IsRequired"] = true,
                    ["Description"] = "The Microsoft Entra tenant identifier.",
                    ["Source"] = "Identity"
                }
            },
            ["version"] = "1.0.0-test",
            ["generatedAt"] = "2026-05-12T00:00:00Z"
        };

        var result = Normalize(HandlebarsTemplateEngine.ProcessTemplateString(templateContent, data));

        // PascalCase Name field in backticks
        Assert.Contains("**`SubscriptionId`**", result);
        Assert.Contains("**`ResourceGroupName`**", result);
        Assert.Contains("**`TenantId`**", result);

        // NL-converted / space-separated names must not appear
        Assert.DoesNotContain("Subscription Id", result);
        Assert.DoesNotContain("Resource Group Name", result);
        Assert.DoesNotContain("Tenant Id", result);
    }

    // ── Template loading helper ─────────────────────────────────────────

    private static async Task<string> LoadTemplateAsync(string templateName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "templates", templateName);
            if (File.Exists(candidate))
                return await File.ReadAllTextAsync(candidate);
            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            $"Template '{templateName}' not found. Searched from {AppContext.BaseDirectory} upward.");
    }
}
