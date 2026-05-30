// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using HorizontalArticleGenerator.Builders;
using HorizontalArticleGenerator.Validation;
using ToolFamilyCleanup.Services;
using ToolGeneration_Improved.Services;
using ToolGeneration_Improved.Validation;

namespace DocGeneration.PromptRegression.Tests.Tests;

/// <summary>
/// Verifies that each reducer/builder produces a correctly-shaped context for all three AI stages.
/// Catches schema regressions: renamed properties, missing fields, unexpected keys, or token-estimate drift.
/// </summary>
public sealed class PromptShapeRegressionTests : IDisposable
{
    private readonly string _testRoot;

    public PromptShapeRegressionTests()
    {
        _testRoot = Path.Combine(AppContext.BaseDirectory, "prompt-shape-regression", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    [Fact]
    public async Task ToolGenerationContext_HasExpectedTopLevelKeys_AndTokenEstimateIsPositive()
    {
        var composedDir = Path.Combine(_testRoot, "composed");
        Directory.CreateDirectory(composedDir);

        const string content = "<!-- @mcpcli fileshares fileshare create -->\n\nCreates a file share with the specified name and quota.";
        await File.WriteAllTextAsync(Path.Combine(composedDir, "fileshares-fileshare-create.md"), content);

        var reducer = new ToolGenerationReducer();
        var context = await reducer.ReduceAsync(composedDir, "fileshares-fileshare-create.md", 4096, CancellationToken.None);

        var json = JsonSerializer.Serialize(context);
        using var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k).ToArray();

        Assert.Equal(
            ["ComposedContent", "MaxTokens", "SchemaVersion", "ToolName"],
            keys);

        var validator = new ToolGenerationBudgetValidator();
        var result = await validator.ValidateAsync(context, CancellationToken.None);
        var expectedTokens = content.Length / 4; // CharsPerToken = 4
        Assert.NotNull(result.EstimatedPromptTokens);
        Assert.InRange(result.EstimatedPromptTokens.Value,
            (int)(expectedTokens * 0.9),
            (int)(expectedTokens * 1.1) + 1);
    }

    [Fact]
    public async Task FamilyStructureContext_HasExpectedTopLevelKeys_AndTokenEstimateIsPositive()
    {
        var toolsDir = Path.Combine(_testRoot, "tools");
        Directory.CreateDirectory(toolsDir);

        const string toolContent = """
            ---
            ---
            # Create disk

            <!-- @mcpcli compute disk create -->

            Creates a managed disk in Azure.
            """;
        await File.WriteAllTextAsync(Path.Combine(toolsDir, "compute-disk-create.md"), toolContent);

        var builder = new FamilyStructureBuilder();
        var context = await builder.BuildAsync(toolsDir, "compute", h2HeadingsDirectory: null, CancellationToken.None);

        var json = JsonSerializer.Serialize(context);
        using var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k).ToArray();

        Assert.Equal(
            ["FamilyName", "SchemaVersion", "Sections"],
            keys);

        // Token estimate: total chars in all section SourceContent / 4
        var totalChars = context.Sections.Sum(s => s.SourceContent.Length);
        Assert.True(totalChars > 0, "Expected non-empty source content in sections.");
        var expectedTokens = totalChars / 4;
        Assert.True(expectedTokens > 0);
    }

    [Fact]
    public async Task ArticleOutlineContext_HasExpectedTopLevelKeys_AndTokenEstimateIsWithinBudget()
    {
        // No cli-output.json needed — builder handles missing file gracefully.
        var builder = new ArticleOutlineBuilder();
        var context = await builder.BuildAsync(_testRoot, "compute", CancellationToken.None);

        var json = JsonSerializer.Serialize(context);
        using var doc = JsonDocument.Parse(json);
        var keys = doc.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(k => k).ToArray();

        Assert.Equal(
            ["ArticleTitle", "SchemaVersion", "Sections", "ServiceIdentifier"],
            keys);

        var validator = new ArticleOutlineBudgetValidator();
        var result = await validator.ValidateAsync(context, CancellationToken.None);
        Assert.NotNull(result.EstimatedPromptTokens);
        Assert.True(result.WithinBudget, $"Token estimate {result.EstimatedPromptTokens} exceeds budget {result.TokenBudget}.");
    }
}
