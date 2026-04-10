// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Tests for StyleGuidePostProcessor — deterministic post-processor that
/// fixes compound word formatting, double-plural errors, and wordy phrases
/// in body text per Microsoft style guide. Fixes: #393
///
/// Three sub-fixers:
///   A. Compound word splitting (data-driven from compound-words.json prose-safe subset)
///   B. Double-plural correction (suffix-based pattern matching)
///   C. Wordy phrase simplification
/// </summary>
public class StyleGuidePostProcessorTests
{
    // ── A. Compound word splitting ──────────────────────────────────

    [Theory]
    [InlineData("The activitylogs show errors.", "The activity logs show errors.")]
    [InlineData("Check the activitylog for details.", "Check the activity log for details.")]
    [InlineData("Configure the eventhub connection.", "Configure the event hub connection.")]
    [InlineData("Review healthmodels before deploying.", "Review health models before deploying.")]
    [InlineData("Create a hostpool for virtual desktops.", "Create a host pool for virtual desktops.")]
    [InlineData("Add a nodepool to the cluster.", "Add a node pool to the cluster.")]
    [InlineData("Run the webtests against the endpoint.", "Run the web tests against the endpoint.")]
    [InlineData("Check bestpractices for guidance.", "Check best practices for guidance.")]
    [InlineData("List the consumergroup settings.", "List the consumer group settings.")]
    public void Fix_CompoundWord_SplitInBodyText(string input, string expected)
    {
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Fix_CompoundWord_CaseInsensitive()
    {
        var input = "The ActivityLog shows the latest entries.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Equal("The Activity log shows the latest entries.", result);
    }

    [Fact]
    public void Fix_CompoundWord_MultipleOccurrences()
    {
        var input = "Use eventhub for streaming and activitylogs for monitoring.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Contains("event hub", result);
        Assert.Contains("activity logs", result);
    }

    [Fact]
    public void Fix_CompoundWord_SkipsShortAbbreviations()
    {
        // "fs", "kv", "iac", "stt", "tts" should NOT be replaced in body text
        var input = "The fs mount and kv store use iac patterns.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_CompoundWord_SkipsAlreadyHyphenated()
    {
        // Already-formatted entries like "app-lens" should not be re-modified
        var input = "Use the app-lens tool for diagnostics.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_CompoundWord_SkipsHeadings()
    {
        // Headings should NOT be modified (PR #392 handles those)
        var input = "## Activitylog\n\nThe activitylog tracks events.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.StartsWith("## Activitylog", result);
        Assert.Contains("The activity log tracks events.", result);
    }

    [Fact]
    public void Fix_CompoundWord_SkipsCodeBlocks()
    {
        var input = "Use the activitylog. ```\nactivitylog --format json\n``` And review activitylogs.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Contains("Use the activity log.", result);
        Assert.Contains("activitylog --format json", result);
        Assert.Contains("review activity logs.", result);
    }

    [Fact]
    public void Fix_CompoundWord_SkipsInlineCode()
    {
        var input = "Run `activitylog` to see the activitylog output.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Contains("`activitylog`", result);
        Assert.Contains("the activity log output", result);
    }

    [Fact]
    public void Fix_CompoundWord_SkipsFrontmatter()
    {
        var input = "---\ntitle: activitylog overview\n---\n\nThe activitylog tracks events.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Contains("title: activitylog overview", result);
        Assert.Contains("The activity log tracks events.", result);
    }

    [Fact]
    public void Fix_CompoundWord_SkipsRuntime()
    {
        // "runtime" is correct per Microsoft style guide — should not be split
        var input = "The runtime version is 3.1.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Equal(input, result);
    }

    // ── B. Double-plural correction ─────────────────────────────────

    [Theory]
    [InlineData("List all accountses.", "List all accounts.")]
    [InlineData("Check the documentses.", "Check the documents.")]
    [InlineData("Deploy to environmentses.", "Deploy to environments.")]
    [InlineData("The endpointses are configured.", "The endpoints are configured.")]
    [InlineData("Update the objectses.", "Update the objects.")]
    [InlineData("Check the resultses.", "Check the results.")]
    [InlineData("Manage defaultses.", "Manage defaults.")]
    [InlineData("List networkses.", "List networks.")]
    [InlineData("View resourceses.", "View resources.")]
    [InlineData("List instanceses.", "List instances.")]
    [InlineData("Check namespaceses.", "Check namespaces.")]
    [InlineData("List databaseses.", "List databases.")]
    public void Fix_DoublePlural_Corrected(string input, string expected)
    {
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Run the processes.")]
    [InlineData("Check the addresses.")]
    [InlineData("List all businesses.")]
    [InlineData("Track the progresses.")]
    [InlineData("Handle the responses.")]
    [InlineData("View the analyses.")]
    [InlineData("Review the diagnoses.")]
    [InlineData("Count the glimpses.")]
    [InlineData("Read the verses.")]
    [InlineData("Browse the courses.")]
    [InlineData("Manage the releases.")]
    [InlineData("See the increases.")]
    [InlineData("Check the licenses.")]
    public void Fix_LegitimateWord_NotModified(string input)
    {
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_DoublePlural_MultipleInText()
    {
        var input = "Query accountses and resourceses in the subscriptions.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Contains("accounts", result);
        Assert.Contains("resources", result);
        Assert.DoesNotContain("accountses", result);
        Assert.DoesNotContain("resourceses", result);
    }

    // ── C. Wordy phrase simplification ──────────────────────────────

    [Theory]
    [InlineData("This tool provides the ability to manage.", "This tool lets you manage.")]
    [InlineData("The service is able to scale.", "The service can scale.")]
    [InlineData("Run this in order to configure.", "Run this to configure.")]
    [InlineData("For the purpose of monitoring, use this.", "To monitoring, use this.")]
    [InlineData("It has the ability to retry.", "It can retry.")]
    [InlineData("It is important to note that values are cached.", "Note that values are cached.")]
    [InlineData("At this point in time the feature is ready.", "Now the feature is ready.")]
    public void Fix_WordyPhrase_Simplified(string input, string expected)
    {
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Fix_WordyPhrase_CaseInsensitive()
    {
        var input = "This Provides The Ability To manage resources.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Contains("Lets you", result);
        Assert.DoesNotContain("provides the ability to", result.ToLowerInvariant());
    }

    [Fact]
    public void Fix_WordyPhrase_SkipsCodeBlocks()
    {
        var input = "```\nprovides the ability to\n```\nThis provides the ability to run.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Contains("provides the ability to", result); // preserved in code block
        Assert.Contains("lets you run.", result); // fixed in body text
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void Fix_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Equal("", StyleGuidePostProcessor.Fix(""));
        Assert.Equal("", StyleGuidePostProcessor.Fix(null!));
    }

    [Fact]
    public void Fix_NoMatchingPatterns_ReturnsUnchanged()
    {
        var input = "This is a normal sentence about Azure resources.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_Idempotent_AlreadyFixed()
    {
        var input = "The activity logs show health models from the event hub.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Equal(input, result);
    }

    [Fact]
    public void Fix_AllThreeFixers_Combined()
    {
        var input = "The activitylogs provides the ability to track accountses.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Contains("activity logs", result);
        Assert.Contains("lets you", result);
        Assert.Contains("accounts", result);
        Assert.DoesNotContain("activitylogs", result);
        Assert.DoesNotContain("provides the ability to", result);
        Assert.DoesNotContain("accountses", result);
    }

    [Fact]
    public void Fix_PreservesAllHeadingLevels()
    {
        var input = "# Activitylog Overview\n## Eventhub Config\n### Healthmodels\n\nThe activitylog data.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Contains("# Activitylog Overview", result);
        Assert.Contains("## Eventhub Config", result);
        Assert.Contains("### Healthmodels", result);
        Assert.Contains("The activity log data.", result);
    }

    [Fact]
    public void Fix_SubnetSize_Split()
    {
        var input = "Calculate the subnetsize for deployment.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Equal("Calculate the subnet size for deployment.", result);
    }

    [Fact]
    public void Fix_MonitoredResources_Split()
    {
        var input = "List all monitoredresources in the subscription.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Equal("List all monitored resources in the subscription.", result);
    }

    [Fact]
    public void Fix_TestRun_Split()
    {
        var input = "Start a testrun for validation.";
        var result = StyleGuidePostProcessor.Fix(input);
        Assert.Equal("Start a test run for validation.", result);
    }
}
