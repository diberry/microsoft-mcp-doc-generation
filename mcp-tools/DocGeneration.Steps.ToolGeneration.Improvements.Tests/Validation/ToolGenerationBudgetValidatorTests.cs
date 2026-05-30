// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolGeneration_Improved.Models;
using ToolGeneration_Improved.Validation;
using Xunit;

namespace DocGeneration.Steps.ToolGeneration.Improvements.Tests.Validation;

public sealed class ToolGenerationBudgetValidatorTests
{
    private readonly ToolGenerationBudgetValidator _sut = new();

    [Fact]
    public async Task SmallContent_ReturnsPass()
    {
        var content = new string('x', 1000); // 1000 chars = 250 estimated tokens
        var context = new ToolGenerationContext("tool create", content, 8000);

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(250, result.EstimatedPromptTokens);
        Assert.Equal(ToolGenerationBudgetValidator.InputTokenBudget, result.TokenBudget);
    }

    [Fact]
    public async Task ContentAtBudget_ReturnsPass()
    {
        // exactly at budget boundary (InputTokenBudget * CharsPerToken chars)
        var content = new string('x', ToolGenerationBudgetValidator.InputTokenBudget * ToolGenerationBudgetValidator.CharsPerToken);
        var context = new ToolGenerationContext("tool create", content, 8000);

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ContentOverBudget_ReturnsFail()
    {
        // one token over budget
        var content = new string('x', (ToolGenerationBudgetValidator.InputTokenBudget + 1) * ToolGenerationBudgetValidator.CharsPerToken);
        var context = new ToolGenerationContext("tool create", content, 8000);

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "ComposedContent");
        Assert.True(result.EstimatedPromptTokens > ToolGenerationBudgetValidator.InputTokenBudget);
    }

    [Fact]
    public async Task OverBudget_IncludesTokenInfo()
    {
        var content = new string('x', (ToolGenerationBudgetValidator.InputTokenBudget + 100) * ToolGenerationBudgetValidator.CharsPerToken);
        var context = new ToolGenerationContext("tool create", content, 8000);

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.Equal(ToolGenerationBudgetValidator.InputTokenBudget, result.TokenBudget);
        Assert.True(result.EstimatedPromptTokens.HasValue);
    }
}
