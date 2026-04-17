// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using TemplateEngine;
using Xunit;

namespace DocGeneration.Steps.AnnotationsParametersRaw.Annotations.Tests;

/// <summary>
/// Regression tests for PR #201 template changes at the template rendering level.
/// These tests render the actual .hbs templates with controlled data and verify
/// the output format matches the expected post-fix behavior.
///
/// PR #201 changes:
///   - tool-family-page.hbs: markers standardized from <!-- {{Command}} --> to <!-- @mcpcli {{Command}} -->
///   - tool-family-page.hbs: blank line between H2 and marker removed
///   - example-prompts-template.hbs: @mcpcli marker removed
///
/// These tests read the actual template from the source tree (mcp-tools/templates/)
/// and render it using HandlebarsTemplateEngine.ProcessTemplateString(). They FAIL if
/// the template changes are reverted.
/// </summary>
public class ToolFamilyPageTemplateRegressionTests
{
    // ── Normalize line endings for cross-platform test reliability ───
    private static string Normalize(string text) => text.Replace("\r\n", "\n");

    // ── @mcpcli marker format: FAIL if reverted to <!-- {{Command}} --> ──

    [Fact]
    public async Task ToolFamilyPage_Markers_UseAtMcpcliPrefix()
    {
        var templateContent = await LoadTemplateAsync("tool-family-page.hbs");
        var data = BuildToolFamilyData(
            areaName: "Azure Storage",
            tools: new[]
            {
                BuildToolData("Account list", "storage account list",
                    description: "Lists all storage accounts.")
            });

        var result = Normalize(HandlebarsTemplateEngine.ProcessTemplateString(templateContent, data));

        Assert.Contains("<!-- @mcpcli storage account list -->", result);
    }

    [Fact]
    public async Task ToolFamilyPage_Markers_DoNotUsePlainCommandFormat()
    {
        var templateContent = await LoadTemplateAsync("tool-family-page.hbs");
        var data = BuildToolFamilyData(
            areaName: "Azure Cosmos DB",
            tools: new[]
            {
                BuildToolData("Account list", "cosmos account list",
                    description: "Lists Cosmos DB accounts.")
            });

        var result = Normalize(HandlebarsTemplateEngine.ProcessTemplateString(templateContent, data));

        // Old format "<!-- cosmos account list -->" must NOT appear
        Assert.DoesNotMatch(@"<!--\s+cosmos account list\s+-->", result);
        // New @mcpcli format must appear
        Assert.Contains("<!-- @mcpcli cosmos account list -->", result);
    }

    [Fact]
    public async Task ToolFamilyPage_MultipleTools_AllMarkersUseAtMcpcliPrefix()
    {
        var templateContent = await LoadTemplateAsync("tool-family-page.hbs");
        var data = BuildToolFamilyData(
            areaName: "Azure Monitor",
            tools: new[]
            {
                BuildToolData("Metric list", "monitor metric list",
                    description: "Lists metrics."),
                BuildToolData("Log query", "monitor log query",
                    description: "Queries logs."),
                BuildToolData("Alert create", "monitor alert create",
                    description: "Creates an alert.")
            });

        var result = Normalize(HandlebarsTemplateEngine.ProcessTemplateString(templateContent, data));

        Assert.Contains("<!-- @mcpcli monitor metric list -->", result);
        Assert.Contains("<!-- @mcpcli monitor log query -->", result);
        Assert.Contains("<!-- @mcpcli monitor alert create -->", result);
    }

    // ── Marker placement: immediately after H2, no blank line ───────
    // Old template had a blank line between ## heading and <!-- marker -->.
    // FAIL if reverted.

    [Fact]
    public async Task ToolFamilyPage_MarkerImmediatelyAfterH2_NoBlankLineBetween()
    {
        var templateContent = await LoadTemplateAsync("tool-family-page.hbs");
        var data = BuildToolFamilyData(
            areaName: "Azure Key Vault",
            tools: new[]
            {
                BuildToolData("Secret list", "keyvault secret list",
                    description: "Lists secrets.")
            });

        var result = Normalize(HandlebarsTemplateEngine.ProcessTemplateString(templateContent, data));

        // Marker immediately after H2 (no blank line)
        Assert.Contains("## Secret list\n<!-- @mcpcli keyvault secret list -->", result);
        // Old format had blank line between heading and marker
        Assert.DoesNotContain("## Secret list\n\n<!--", result);
    }

    // ── Marker count: exactly one per tool section ──────────────────

    [Fact]
    public async Task ToolFamilyPage_EachToolSection_HasExactlyOneCommentMarker()
    {
        var templateContent = await LoadTemplateAsync("tool-family-page.hbs");
        var data = BuildToolFamilyData(
            areaName: "Azure Storage",
            tools: new[]
            {
                BuildToolData("Account list", "storage account list",
                    description: "Lists storage accounts."),
                BuildToolData("Container create", "storage container create",
                    description: "Creates a storage container."),
                BuildToolData("Blob delete", "storage blob delete",
                    description: "Deletes a blob.")
            });

        var result = HandlebarsTemplateEngine.ProcessTemplateString(templateContent, data);

        var mcpCliMarkers = Regex.Matches(result, @"<!-- @mcpcli .+? -->");
        Assert.Equal(3, mcpCliMarkers.Count);
    }

    [Fact]
    public async Task ToolFamilyPage_NoToolSection_HasDuplicateMarkers()
    {
        var templateContent = await LoadTemplateAsync("tool-family-page.hbs");
        var data = BuildToolFamilyData(
            areaName: "Azure Monitor",
            tools: new[]
            {
                BuildToolData("Metric list", "monitor metric list",
                    description: "Lists available metrics for a resource.")
            });

        var result = Normalize(HandlebarsTemplateEngine.ProcessTemplateString(templateContent, data));

        var toolSectionStart = result.IndexOf("## Metric list");
        Assert.True(toolSectionStart >= 0, "Tool H2 heading not found in rendered output");

        var toolSection = result.Substring(toolSectionStart);
        var nextH2 = toolSection.IndexOf("\n## ", 3);
        if (nextH2 > 0) toolSection = toolSection.Substring(0, nextH2);

        var commandMarkers = Regex.Matches(toolSection, @"<!-- @mcpcli monitor metric list -->")
            .Count;
        Assert.Equal(1, commandMarkers);
    }

    // ── example-prompts-template.hbs: @mcpcli marker removed ────────
    // FAIL if the @mcpcli marker removal is reverted.

    [Fact]
    public async Task ExamplePromptsTemplate_DoesNotContainMcpcliMarker()
    {
        var templateContent = await LoadTemplateAsync("example-prompts-template.hbs");
        var data = new Dictionary<string, object>
        {
            ["command"] = "storage account list",
            ["version"] = "1.0.0-test",
            ["generatedAt"] = "2026-03-23T00:00:00Z",
            ["examplePrompts"] = new[]
            {
                new Dictionary<string, object> { ["text"] = "List all storage accounts" },
                new Dictionary<string, object> { ["text"] = "Show my storage accounts" }
            }
        };

        var result = HandlebarsTemplateEngine.ProcessTemplateString(templateContent, data);

        Assert.DoesNotContain("@mcpcli", result);
        Assert.DoesNotContain("<!-- @mcpcli", result);
    }

    // ── Template loading helper ─────────────────────────────────────────

    /// <summary>
    /// Searches up from the test output directory to find the templates/ directory.
    /// </summary>
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

    // ── Data builders ───────────────────────────────────────────────────

    private static Dictionary<string, object> BuildToolFamilyData(
        string areaName,
        Dictionary<string, object>[] tools)
    {
        return new Dictionary<string, object>
        {
            ["areaName"] = areaName,
            ["areaDescription"] = $"Tools for {areaName}.",
            ["toolCount"] = tools.Length,
            ["tools"] = tools,
            ["version"] = "1.0.0-test",
            ["generatedAt"] = "2026-03-23T00:00:00Z"
        };
    }

    private static Dictionary<string, object> BuildToolData(
        string name,
        string command,
        string? description = null)
    {
        return new Dictionary<string, object>
        {
            ["Name"] = name,
            ["Command"] = command,
            ["Description"] = description ?? $"Test description for {command}."
        };
    }
}
