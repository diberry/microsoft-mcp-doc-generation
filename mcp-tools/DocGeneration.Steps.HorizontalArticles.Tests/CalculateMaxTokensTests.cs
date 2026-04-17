// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using ArticleGenerator = HorizontalArticleGenerator.Generators.HorizontalArticleGenerator;

namespace HorizontalArticleGenerator.Tests;

public class CalculateMaxTokensTests
{
    [Theory]
    [InlineData(1, 8000)]
    [InlineData(0, 8000)]
    [InlineData(7, 8200)]
    [InlineData(10, 10000)]
    [InlineData(35, 24000)]
    public void CalculateMaxTokens_ReturnsExpectedValue(int toolCount, int expected)
    {
        var result = ArticleGenerator.CalculateMaxTokens(toolCount);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void CalculateMaxTokens_NeverBelowMinimum()
    {
        for (var i = 0; i <= 6; i++)
        {
            Assert.True(ArticleGenerator.CalculateMaxTokens(i) >= 8000);
        }
    }

    [Fact]
    public void CalculateMaxTokens_NeverAboveMaximum()
    {
        for (var i = 30; i <= 100; i++)
        {
            Assert.True(ArticleGenerator.CalculateMaxTokens(i) <= 24000);
        }
    }
}
