// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

public class CliMarkerReconcilerTests
{
    [Fact]
    public void Reconcile_FixesCorruptedMarker()
    {
        var markdown = "## Activity log: List\n\n<!-- @mcpcli monitor activity log list -->\n\nDescription here.";
        var commands = new List<string?> { "monitor activitylog list" };
        var result = CliMarkerReconciler.ReconcileAndInject(markdown, commands);
        Assert.Contains("<!-- @mcpcli monitor activitylog list -->", result);
        Assert.DoesNotContain("activity log list", result);
    }

    [Fact]
    public void Reconcile_PreservesCorrectMarker()
    {
        var markdown = "## Metrics: Query\n\n<!-- @mcpcli monitor metrics query -->\n\nDescription.";
        var commands = new List<string?> { "monitor metrics query" };
        var result = CliMarkerReconciler.ReconcileAndInject(markdown, commands);
        Assert.Contains("<!-- @mcpcli monitor metrics query -->", result);
    }

    [Fact]
    public void Reconcile_InjectsMissingMarker()
    {
        var markdown = "## Activity log: List\n\nDescription without marker.";
        var commands = new List<string?> { "monitor activitylog list" };
        var result = CliMarkerReconciler.ReconcileAndInject(markdown, commands);
        Assert.Contains("<!-- @mcpcli monitor activitylog list -->", result);
    }

    [Fact]
    public void Reconcile_SkipsNullCommands()
    {
        var markdown = "## Tool One\n\n<!-- @mcpcli foo bar -->\n\n## Tool Two\n\nNo marker.";
        var commands = new List<string?> { null, "baz qux" };
        var result = CliMarkerReconciler.ReconcileAndInject(markdown, commands);
        Assert.Contains("<!-- @mcpcli foo bar -->", result);
        Assert.Contains("<!-- @mcpcli baz qux -->", result);
    }

    [Fact]
    public void Reconcile_HandlesMultipleMarkers()
    {
        var markdown = @"## Tool A

<!-- @mcpcli monitor activity log list -->

Desc A.

## Tool B

<!-- @mcpcli monitor health models entity get -->

Desc B.

## Related content

Links here.";
        var commands = new List<string?> { "monitor activitylog list", "monitor healthmodels entity get" };
        var result = CliMarkerReconciler.ReconcileAndInject(markdown, commands);
        Assert.Contains("<!-- @mcpcli monitor activitylog list -->", result);
        Assert.Contains("<!-- @mcpcli monitor healthmodels entity get -->", result);
        Assert.DoesNotContain("activity log list", result);
        Assert.DoesNotContain("health models entity", result);
    }

    [Fact]
    public void Reconcile_SkipsRelatedContentH2()
    {
        var markdown = "## Tool A\n\n<!-- @mcpcli foo bar -->\n\nDesc.\n\n## Related content\n\nLinks.";
        var commands = new List<string?> { "foo bar" };
        var result = CliMarkerReconciler.ReconcileAndInject(markdown, commands);
        var relatedIdx = result.IndexOf("## Related content");
        var afterRelated = result[(relatedIdx + "## Related content".Length)..];
        Assert.DoesNotContain("@mcpcli", afterRelated);
    }

    [Fact]
    public void Reconcile_EmptyMarkdownReturnsUnchanged()
    {
        var result = CliMarkerReconciler.ReconcileAndInject("", new List<string?> { "foo" });
        Assert.Equal("", result);
    }

    [Fact]
    public void Reconcile_EmptyCommandsReturnsUnchanged()
    {
        var markdown = "## Tool\n\n<!-- @mcpcli foo bar -->\n\nDesc.";
        var result = CliMarkerReconciler.ReconcileAndInject(markdown, new List<string?>());
        Assert.Equal(markdown, result);
    }

    [Fact]
    public void Reconcile_ExtraWhitespaceInMarker()
    {
        var markdown = "## Tool\n\n<!--  @mcpcli  monitor  activitylog  list  -->\n\nDesc.";
        var commands = new List<string?> { "monitor activitylog list" };
        var result = CliMarkerReconciler.ReconcileAndInject(markdown, commands);
        Assert.Contains("<!-- @mcpcli monitor activitylog list -->", result);
    }
}
