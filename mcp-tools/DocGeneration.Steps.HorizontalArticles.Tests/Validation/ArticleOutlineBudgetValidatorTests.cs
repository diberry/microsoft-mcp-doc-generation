// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using HorizontalArticleGenerator.Models;
using HorizontalArticleGenerator.Validation;
using Xunit;

namespace DocGeneration.Steps.HorizontalArticles.Tests.Validation;

public sealed class ArticleOutlineBudgetValidatorTests
{
    private readonly ArticleOutlineBudgetValidator _sut = new();

    private static ArticleOutlineSection MakeSection(string heading, int evidenceLength)
        => new(heading, "howto", [new string('x', evidenceLength)]);

    [Fact]
    public async Task SmallContext_ReturnsPass()
    {
        var context = new ArticleOutlineContext(
            "Title",
            [MakeSection("A", 100), MakeSection("B", 100)],
            "azure-mcp");

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(50, result.EstimatedPromptTokens); // (100 + 100) / 4
        Assert.Equal(ArticleOutlineBudgetValidator.InputTokenBudget, result.TokenBudget);
    }

    [Fact]
    public async Task ContextAtBudgetBoundary_ReturnsPass()
    {
        var totalChars = ArticleOutlineBudgetValidator.InputTokenBudget * ArticleOutlineBudgetValidator.CharsPerToken;
        var context = new ArticleOutlineContext(
            "Title",
            [MakeSection("A", totalChars)],
            "azure-mcp");

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ContextOverBudget_ReturnsFail()
    {
        var overBudgetChars = (ArticleOutlineBudgetValidator.InputTokenBudget + 1) * ArticleOutlineBudgetValidator.CharsPerToken;
        var context = new ArticleOutlineContext(
            "Title",
            [MakeSection("A", overBudgetChars)],
            "azure-mcp");

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Sections");
        Assert.True(result.EstimatedPromptTokens > ArticleOutlineBudgetValidator.InputTokenBudget);
    }

    [Fact]
    public async Task ContextOverBudget_IncludesTokenInfo()
    {
        var overBudgetChars = (ArticleOutlineBudgetValidator.InputTokenBudget + 500) * ArticleOutlineBudgetValidator.CharsPerToken;
        var context = new ArticleOutlineContext(
            "Title",
            [MakeSection("A", overBudgetChars)],
            "azure-mcp");

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.Equal(ArticleOutlineBudgetValidator.InputTokenBudget, result.TokenBudget);
        Assert.True(result.EstimatedPromptTokens.HasValue);
    }

    [Fact]
    public async Task EvidenceAcrossMultipleSections_CombinedForEstimate()
    {
        // 3 sections × 400 chars each = 1200 chars / 4 = 300 tokens (well within budget)
        var context = new ArticleOutlineContext(
            "Title",
            [MakeSection("A", 400), MakeSection("B", 400), MakeSection("C", 400)],
            "azure-mcp");

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(300, result.EstimatedPromptTokens);
    }
}
