// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using Xunit;

namespace DocGeneration.Core.GenerativeAI.Tests;

public class CalculateDynamicMaxTokensTests
{
    [Fact]
    public void ReturnsMinimum_WhenPromptsAreVeryShort()
    {
        var result = GenerativeAIClient.CalculateDynamicMaxTokens("short", "hi");
        Assert.Equal(1500, result);
    }

    [Fact]
    public void ScalesWithPromptLength()
    {
        var system = new string('x', 4000); // ~1000 tokens
        var user = new string('y', 4000);   // ~1000 tokens
        // 2000 estimated input * 2.0 multiplier = 4000
        var result = GenerativeAIClient.CalculateDynamicMaxTokens(system, user);
        Assert.Equal(4000, result);
    }

    [Fact]
    public void RespectsMaximumCeiling()
    {
        var system = new string('x', 40000); // ~10000 tokens
        var user = new string('y', 40000);   // ~10000 tokens
        // 20000 * 2.0 = 40000, but capped at 16384
        var result = GenerativeAIClient.CalculateDynamicMaxTokens(system, user);
        Assert.Equal(16384, result);
    }

    [Fact]
    public void RespectsCustomMinimum()
    {
        var result = GenerativeAIClient.CalculateDynamicMaxTokens("hi", "hi", minimumTokens: 500);
        Assert.Equal(500, result);
    }

    [Fact]
    public void RespectsCustomMaximum()
    {
        var system = new string('x', 40000);
        var user = new string('y', 40000);
        var result = GenerativeAIClient.CalculateDynamicMaxTokens(system, user, maximumTokens: 8000);
        Assert.Equal(8000, result);
    }

    [Fact]
    public void CustomMultiplier_AffectsResult()
    {
        var system = new string('x', 4000); // ~1000 tokens
        var user = new string('y', 4000);   // ~1000 tokens
        // 2000 * 3.0 = 6000
        var result = GenerativeAIClient.CalculateDynamicMaxTokens(system, user, outputMultiplier: 3.0);
        Assert.Equal(6000, result);
    }

    [Fact]
    public void ServiceDescriptionPrompt_NeverHitsOld1000Limit()
    {
        // Simulates the service-description prompts that were causing the truncation
        var systemPrompt = new string('x', 1200); // ~300 tokens (realistic system prompt)
        var userPrompt = new string('y', 400);    // ~100 tokens (realistic user prompt)
        // With 3x multiplier (as FamilyMetadataGenerator uses): 400 * 3 = 1200, clamped to min 1500
        var result = GenerativeAIClient.CalculateDynamicMaxTokens(
            systemPrompt, userPrompt, outputMultiplier: 3.0, minimumTokens: 1500);
        Assert.True(result >= 1500, $"Expected >= 1500 but got {result}");
    }

    [Theory]
    [InlineData(100, 100)]    // tiny prompts
    [InlineData(2000, 2000)]  // medium prompts
    [InlineData(8000, 8000)]  // large prompts
    [InlineData(50000, 50000)] // huge prompts
    public void AlwaysReturnsWithinBounds(int systemLen, int userLen)
    {
        var system = new string('x', systemLen);
        var user = new string('y', userLen);
        var result = GenerativeAIClient.CalculateDynamicMaxTokens(system, user);
        Assert.InRange(result, 1500, 16384);
    }
}
