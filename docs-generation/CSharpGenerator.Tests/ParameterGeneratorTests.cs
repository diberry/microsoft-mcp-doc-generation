// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using CSharpGenerator.Generators;
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Tests ParameterGenerator.BuildRequiredText.
/// Priority: P0 — parameter generation is Step 1 of the pipeline.
/// </summary>
public class ParameterGeneratorTests
{
    // ── BuildRequiredText ──────────────────────────────────────

    [Fact]
    public void BuildRequiredText_Required_ReturnsRequired()
    {
        var result = ParameterGenerator.BuildRequiredText(
            true, "--name", new HashSet<string>());

        Assert.Equal("Required", result);
    }

    [Fact]
    public void BuildRequiredText_Optional_ReturnsOptional()
    {
        var result = ParameterGenerator.BuildRequiredText(
            false, "--name", new HashSet<string>());

        Assert.Equal("Optional", result);
    }

    [Fact]
    public void BuildRequiredText_ConditionalRequired_AppendsStar()
    {
        var conditionals = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--account-name" };
        var result = ParameterGenerator.BuildRequiredText(
            false, "--account-name", conditionals);

        Assert.Equal("Optional*", result);
    }

    [Fact]
    public void BuildRequiredText_ConditionalRequired_Required_AppendsStar()
    {
        var conditionals = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--vault-name" };
        var result = ParameterGenerator.BuildRequiredText(
            true, "--vault-name", conditionals);

        Assert.Equal("Required*", result);
    }

    [Fact]
    public void BuildRequiredText_NotInConditionalSet_NoStar()
    {
        var conditionals = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--other" };
        var result = ParameterGenerator.BuildRequiredText(
            false, "--name", conditionals);

        Assert.Equal("Optional", result);
    }

    [Fact]
    public void BuildRequiredText_EmptyConditionalSet_NoStar()
    {
        var result = ParameterGenerator.BuildRequiredText(
            true, "--name", new HashSet<string>());

        Assert.Equal("Required", result);
    }

    [Fact]
    public void BuildRequiredText_CaseInsensitiveConditionalMatch()
    {
        // Production code creates the HashSet with OrdinalIgnoreCase.
        // Verify that case differences in param name still match.
        var conditionals = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "--Account-Name" };
        var result = ParameterGenerator.BuildRequiredText(
            false, "--account-name", conditionals);

        Assert.Equal("Optional*", result);
    }

    [Fact]
    public void BuildRequiredText_CaseSensitiveSet_DoesNotMatch()
    {
        // When caller uses default (ordinal) comparer, case mismatch → no star.
        // Documents that correctness depends on the caller's comparer.
        var conditionals = new HashSet<string> { "--Account-Name" };
        var result = ParameterGenerator.BuildRequiredText(
            false, "--account-name", conditionals);

        Assert.Equal("Optional", result);
    }
}
