// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using NUnit.Framework;
using ArticleGenerator = HorizontalArticleGenerator.Generators.HorizontalArticleGenerator;

namespace HorizontalArticleGenerator.Tests;

[TestFixture]
public class CalculateMaxTokensTests
{
    [TestCase(1, 8000, Description = "1 tool: 4000+600=4600 → clamped to min 8000")]
    [TestCase(0, 8000, Description = "0 tools: 4000+0=4000 → clamped to min 8000")]
    [TestCase(7, 8200, Description = "7 tools: 4000+4200=8200 → within range")]
    [TestCase(10, 10000, Description = "10 tools: 4000+6000=10000 → within range")]
    [TestCase(35, 24000, Description = "35 tools: 4000+21000=25000 → clamped to max 24000")]
    public void CalculateMaxTokens_ReturnsExpectedValue(int toolCount, int expected)
    {
        var result = ArticleGenerator.CalculateMaxTokens(toolCount);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void CalculateMaxTokens_NeverBelowMinimum()
    {
        for (var i = 0; i <= 6; i++)
        {
            Assert.That(ArticleGenerator.CalculateMaxTokens(i), Is.GreaterThanOrEqualTo(8000));
        }
    }

    [Test]
    public void CalculateMaxTokens_NeverAboveMaximum()
    {
        for (var i = 30; i <= 100; i++)
        {
            Assert.That(ArticleGenerator.CalculateMaxTokens(i), Is.LessThanOrEqualTo(24000));
        }
    }
}
