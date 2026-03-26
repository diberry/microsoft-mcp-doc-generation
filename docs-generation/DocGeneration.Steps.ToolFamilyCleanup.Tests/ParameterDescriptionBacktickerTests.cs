// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// TDD Red Phase tests for <see cref="ParameterDescriptionBackticker"/>.
/// Validates that technical values in parameter descriptions are wrapped in backticks.
/// Uses diverse Azure service examples per AD-008.
/// </summary>
public class ParameterDescriptionBacktickerTests
{
    // ───────────────────────────────────────────────
    // (a) Enum values
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData(
        "Authentication method. Options: Credential, Key, ConnectionString.",
        "Authentication method. Options: `Credential`, `Key`, `ConnectionString`.")]
    [InlineData(
        "Retry strategy: Fixed or Exponential.",
        "Retry strategy: `Fixed` or `Exponential`.")]
    public void BackticksEnumValues(string input, string expected)
    {
        var result = ParameterDescriptionBackticker.Apply(input);
        Assert.Equal(expected, result);
    }

    // ───────────────────────────────────────────────
    // (b) Boolean literals
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData(
        "Set to true to enable verbose logging.",
        "Set to `true` to enable verbose logging.")]
    [InlineData(
        "When false, skips validation.",
        "When `false`, skips validation.")]
    [InlineData(
        "Defaults to true or false depending on context.",
        "Defaults to `true` or `false` depending on context.")]
    public void BackticksBooleanLiterals(string input, string expected)
    {
        var result = ParameterDescriptionBackticker.Apply(input);
        Assert.Equal(expected, result);
    }

    // ───────────────────────────────────────────────
    // (c) Date formats
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData(
        "Date format: YYYY-MM-DD.",
        "Date format: `YYYY-MM-DD`.")]
    [InlineData(
        "Use yyyy-MM-ddTHH:mm:ss for timestamps.",
        "Use `yyyy-MM-ddTHH:mm:ss` for timestamps.")]
    [InlineData(
        "Filter by date (ISO 8601 timestamp).",
        "Filter by date (ISO 8601 timestamp).")]
    public void BackticksDateFormats(string input, string expected)
    {
        var result = ParameterDescriptionBackticker.Apply(input);
        Assert.Equal(expected, result);
    }

    // ───────────────────────────────────────────────
    // (d) CLI switches
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData(
        "Use --verbose flag for detailed output.",
        "Use `--verbose` flag for detailed output.")]
    [InlineData(
        "Pass --no-cache to skip caching.",
        "Pass `--no-cache` to skip caching.")]
    public void BackticksCliSwitches(string input, string expected)
    {
        var result = ParameterDescriptionBackticker.Apply(input);
        Assert.Equal(expected, result);
    }

    // ───────────────────────────────────────────────
    // (e) Known Azure enum patterns (PascalCase/underscore)
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData(
        "SKU options: Standard_LRS, Premium_LRS, Standard_GRS.",
        "SKU options: `Standard_LRS`, `Premium_LRS`, `Standard_GRS`.")]
    [InlineData(
        "Consistency level: BoundedStaleness, Session, Eventual.",
        "Consistency level: `BoundedStaleness`, `Session`, `Eventual`.")]
    public void BackticksKnownAzureEnums(string input, string expected)
    {
        var result = ParameterDescriptionBackticker.Apply(input);
        Assert.Equal(expected, result);
    }

    // ───────────────────────────────────────────────
    // (f) Already backticked — no double-wrapping
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData("Use `true` to enable.")]
    [InlineData("Options: `Credential`, `Key`, `ConnectionString`.")]
    [InlineData("Format: `YYYY-MM-DD`.")]
    [InlineData("Pass `--verbose` for details.")]
    public void PreservesAlreadyBackticked(string input)
    {
        var result = ParameterDescriptionBackticker.Apply(input);
        Assert.Equal(input, result);
    }

    // ───────────────────────────────────────────────
    // (g) Normal prose — nothing backticked
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData("The name of the resource group.")]
    [InlineData("Specifies the Azure subscription to use.")]
    [InlineData("Maximum number of results to return.")]
    [InlineData("The display name for the Cosmos DB account.")]
    public void PreservesNormalProse(string input)
    {
        var result = ParameterDescriptionBackticker.Apply(input);
        Assert.Equal(input, result);
    }

    // ───────────────────────────────────────────────
    // (h) Azure service names — not backticked
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData("Azure Storage account name.")]
    [InlineData("Azure Key Vault secret identifier.")]
    [InlineData("Azure Cosmos DB database name.")]
    [InlineData("Azure SQL Server firewall rule.")]
    [InlineData("Azure Virtual Machine scale set.")]
    public void PreservesAzureServiceNames(string input)
    {
        var result = ParameterDescriptionBackticker.Apply(input);
        Assert.Equal(input, result);
    }

    // ───────────────────────────────────────────────
    // (i) Multiple patterns in one line
    // ───────────────────────────────────────────────

    [Fact]
    public void HandlesMultiplePatternsInOneLine()
    {
        var input = "Set to true, use format YYYY-MM-DD, retry mode: Fixed or Exponential, pass --dry-run.";
        var result = ParameterDescriptionBackticker.Apply(input);

        Assert.Contains("`true`", result);
        Assert.Contains("`YYYY-MM-DD`", result);
        Assert.Contains("`Fixed`", result);
        Assert.Contains("`Exponential`", result);
        Assert.Contains("`--dry-run`", result);
        // Ensure no double backticks
        Assert.DoesNotContain("``", result);
    }

    // ───────────────────────────────────────────────
    // (j) Null or empty input
    // ───────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrEmptyInput(string? input)
    {
        var result = ParameterDescriptionBackticker.Apply(input!);
        Assert.Equal(input, result);
    }
}
