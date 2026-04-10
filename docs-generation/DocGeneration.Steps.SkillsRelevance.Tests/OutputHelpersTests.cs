// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using SkillsRelevance.Output;

namespace SkillsRelevance.Tests;

public class OutputHelpersTests
{
    // ── GetRelevanceLevel ────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0, "high")]
    [InlineData(0.8, "high")]
    [InlineData(0.79, "medium")]
    [InlineData(0.5, "medium")]
    [InlineData(0.49, "low")]
    [InlineData(0.2, "low")]
    [InlineData(0.19, "minimal")]
    [InlineData(0.0, "minimal")]
    public void GetRelevanceLevel_ReturnsCorrectLevel(double score, string expected)
    {
        Assert.Equal(expected, OutputHelpers.GetRelevanceLevel(score));
    }

    // ── SanitizeFileName ─────────────────────────────────────────────────

    [Theory]
    [InlineData("storage", "storage")]
    [InlineData("Azure Key Vault", "azure-key-vault")]
    [InlineData("cosmos-db", "cosmos-db")]
    [InlineData("UPPER_CASE", "uppercase")]
    [InlineData("special!@#chars", "specialchars")]
    public void SanitizeFileName_ReturnsExpected(string input, string expected)
    {
        Assert.Equal(expected, OutputHelpers.SanitizeFileName(input));
    }
}
