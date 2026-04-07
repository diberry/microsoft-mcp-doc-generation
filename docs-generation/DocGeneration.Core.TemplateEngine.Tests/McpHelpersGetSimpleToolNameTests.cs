// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using TemplateEngine;
using TemplateEngine.Helpers;
using Xunit;

namespace TemplateEngine.Tests;

public class McpHelpersGetSimpleToolNameTests
{
    private static readonly Dictionary<string, string> CompoundWords = new()
    {
        ["activitylog"] = "activity-log",
        ["loganalytics"] = "log-analytics",
        ["consumergroup"] = "consumer-group",
        ["healthmodels"] = "health-models",
        ["bestpractices"] = "best-practices",
        ["eventhub"] = "event-hub",
        ["monitoredresources"] = "monitored-resources",
        ["hostpool"] = "host-pool",
        ["nodepool"] = "node-pool",
        ["webtests"] = "web-tests",
        ["subnetsize"] = "subnet-size",
        ["testresource"] = "test-resource",
        ["testrun"] = "test-run",
    };

    // Bug 1: H2s should start with an action verb
    [Theory]
    [InlineData("monitor activitylog list", "List")]
    [InlineData("storage blob create", "Create")]
    [InlineData("cosmos container delete", "Delete")]
    [InlineData("monitor loganalytics query", "Query")]
    [InlineData("compute vm show", "Get")]
    [InlineData("compute vm describe", "Get")]
    [InlineData("storage blob remove", "Delete")]
    [InlineData("storage account add", "Create")]
    public void StartsWithVerb(string command, string expectedVerb)
    {
        var result = McpHelpers.GenerateSimpleToolName(command, CompoundWords);
        Assert.StartsWith(expectedVerb, result);
    }

    // Bug 2: Compound CLI commands are split into readable words
    [Theory]
    [InlineData("monitor activitylog list", "Activity")]
    [InlineData("monitor loganalytics query", "Log")]
    [InlineData("monitor loganalytics query", "Analytics")]
    [InlineData("eventhub consumergroup list", "Consumer")]
    [InlineData("monitor healthmodels get", "Health")]
    [InlineData("monitor healthmodels get", "Models")]
    public void SplitsCompoundWords(string command, string expectedContains)
    {
        var result = McpHelpers.GenerateSimpleToolName(command, CompoundWords);
        Assert.Contains(expectedContains, result);
    }

    // Bug 3: No area prefix or colon pattern in output
    [Theory]
    [InlineData("workbooks workbooks list")]
    [InlineData("monitor activitylog list")]
    [InlineData("storage blob create")]
    public void NoAreaPrefixOrColon(string command)
    {
        var result = McpHelpers.GenerateSimpleToolName(command, CompoundWords);
        Assert.DoesNotContain(":", result);
    }

    // Full expected output verification
    [Theory]
    [InlineData("monitor activitylog list", "List Activity Logs")]
    [InlineData("monitor loganalytics query", "Query Log Analytics")]
    [InlineData("storage blob list", "List Blobs")]
    [InlineData("storage blob create", "Create Blob")]
    [InlineData("compute vm show", "Get Vm")]
    [InlineData("cosmos container delete", "Delete Container")]
    [InlineData("eventhub consumergroup list", "List Consumer Groups")]
    [InlineData("compute disk create", "Create Disk")]
    [InlineData("workbooks workbooks list", "List Workbooks")]
    public void FullExpectedOutput(string command, string expected)
    {
        var result = McpHelpers.GenerateSimpleToolName(command, CompoundWords);
        Assert.Equal(expected, result);
    }

    // Pluralization for "list" verb
    [Theory]
    [InlineData("storage blob list", "Blobs")]
    [InlineData("monitor activitylog list", "Logs")]
    [InlineData("eventhub consumergroup list", "Groups")]
    public void ListVerbPluralizesResource(string command, string expectedEnding)
    {
        var result = McpHelpers.GenerateSimpleToolName(command, CompoundWords);
        Assert.EndsWith(expectedEnding, result);
    }

    // Non-list verbs do NOT pluralize
    [Theory]
    [InlineData("storage blob create", "Blob")]
    [InlineData("cosmos container delete", "Container")]
    [InlineData("compute vm show", "Vm")]
    public void NonListVerbDoesNotPluralize(string command, string expectedEnding)
    {
        var result = McpHelpers.GenerateSimpleToolName(command, CompoundWords);
        Assert.EndsWith(expectedEnding, result);
    }

    // Two-part commands: area + verb
    [Theory]
    [InlineData("advisor list", "List")]
    [InlineData("cosmos query", "Query")]
    [InlineData("storage get", "Get")]
    public void TwoPartVerbOnly(string command, string expected)
    {
        var result = McpHelpers.GenerateSimpleToolName(command, CompoundWords);
        Assert.Equal(expected, result);
    }

    // Two-part commands: area + resource (no verb)
    [Theory]
    [InlineData("advisor recommendations", "Recommendations")]
    [InlineData("monitor diagnostics", "Diagnostics")]
    public void TwoPartResourceOnly(string command, string expected)
    {
        var result = McpHelpers.GenerateSimpleToolName(command, CompoundWords);
        Assert.Equal(expected, result);
    }

    // Two-part with compound word and no verb
    [Theory]
    [InlineData("monitor activitylog", "Activity Log")]
    [InlineData("monitor healthmodels", "Health Models")]
    public void TwoPartCompoundWithoutVerb(string command, string expected)
    {
        var result = McpHelpers.GenerateSimpleToolName(command, CompoundWords);
        Assert.Equal(expected, result);
    }

    // Edge cases
    [Theory]
    [InlineData(null, "Unknown")]
    [InlineData("", "Unknown")]
    [InlineData("single", "Unknown")]
    [InlineData("  ", "Unknown")]
    public void EdgeCases(string? command, string expected)
    {
        var result = McpHelpers.GenerateSimpleToolName(command!, CompoundWords);
        Assert.Equal(expected, result);
    }

    // Without compound words, compound words stay as one token
    [Fact]
    public void WithoutCompoundWords_NoSplitting()
    {
        var result = McpHelpers.GenerateSimpleToolName("monitor activitylog list", null);
        Assert.StartsWith("List", result);
        Assert.Contains("Activitylog", result);
    }

    // Hyphenated resource parts are split into separate words
    [Fact]
    public void HyphenatedResourcePartsFormattedAsWords()
    {
        var result = McpHelpers.GenerateSimpleToolName("compute managed-disk create", CompoundWords);
        Assert.Equal("Create Managed Disk", result);
    }

    // Verb-first detection when verb is the first remaining part
    [Fact]
    public void VerbFirstPattern_DetectsVerbInFirstPosition()
    {
        var result = McpHelpers.GenerateSimpleToolName("advisor list recommendations", CompoundWords);
        Assert.StartsWith("List", result);
    }

    // Handlebars helper integration test
    [Fact]
    public void HandlebarsHelper_ReturnsCorrectOutput()
    {
        var data = new Dictionary<string, object>
        {
            ["command"] = "monitor activitylog list"
        };
        var result = HandlebarsTemplateEngine.ProcessTemplateString(
            "{{getSimpleToolName command}}", data);
        Assert.StartsWith("List", result);
    }
}
