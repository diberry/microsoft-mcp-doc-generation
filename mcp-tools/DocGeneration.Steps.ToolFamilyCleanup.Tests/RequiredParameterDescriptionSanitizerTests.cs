// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// TDD tests for <see cref="RequiredParameterDescriptionSanitizer"/>.
/// Validates that "Defaults to..." language is stripped from Required parameter descriptions.
/// Uses diverse Azure service examples per the universal design principle.
/// </summary>
public class RequiredParameterDescriptionSanitizerTests
{
    // ── Optional parameter — no change ────────────────────────────────────────

    [Fact]
    public void OptionalParameter_PreservesDefaultsLanguage()
    {
        var input = "The VM image to use. Defaults to Ubuntu 24.04 LTS.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: false);
        Assert.Equal(input, result);
    }

    [Fact]
    public void OptionalParameter_PreservesDefaultIsLanguage()
    {
        var input = "Consistency level for Cosmos DB. Default is Session.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: false);
        Assert.Equal(input, result);
    }

    // ── Required — "Defaults to {value}." ─────────────────────────────────────

    [Fact]
    public void RequiredParameter_StripsDefaultsToSentence()
    {
        var input = "The VM image alias. Defaults to Ubuntu 24.04 LTS.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: true);
        Assert.Equal("The VM image alias.", result);
        Assert.DoesNotContain("Defaults to", result);
    }

    [Fact]
    public void RequiredParameter_StripsDefaultsToIfNotSpecified()
    {
        var input = "Storage account SKU. Defaults to Standard_LRS if not specified.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: true);
        Assert.Equal("Storage account SKU.", result);
        Assert.DoesNotContain("Defaults to", result);
    }

    [Fact]
    public void RequiredParameter_StripsDefaultsToWhenNotProvided()
    {
        var input = "Key Vault secret version. Defaults to the latest version when not provided.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: true);
        Assert.Equal("Key Vault secret version.", result);
        Assert.DoesNotContain("Defaults to", result);
    }

    [Fact]
    public void RequiredParameter_StripsDefaultsToUnlessSpecified()
    {
        var input = "The AKS node pool count. Defaults to 3 unless specified.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: true);
        Assert.Equal("The AKS node pool count.", result);
        Assert.DoesNotContain("Defaults to", result);
    }

    // ── Required — "Default is {value}." ──────────────────────────────────────

    [Fact]
    public void RequiredParameter_StripsDefaultIsSentence()
    {
        var input = "Upgrade policy mode for the scale set. Default is Manual.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: true);
        Assert.Equal("Upgrade policy mode for the scale set.", result);
        Assert.DoesNotContain("Default is", result);
    }

    // ── Required — "Default: {value}." ────────────────────────────────────────

    [Fact]
    public void RequiredParameter_StripsDefaultColonSentence()
    {
        var input = "SQL Server authentication type. Default: SqlAuthentication.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: true);
        Assert.Equal("SQL Server authentication type.", result);
        Assert.DoesNotContain("Default:", result);
    }

    // ── Required — "If not specified, defaults to {value}." ───────────────────

    [Fact]
    public void RequiredParameter_StripsIfNotSpecifiedDefaults()
    {
        var input = "The App Service plan tier. If not specified, defaults to Free.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: true);
        Assert.Equal("The App Service plan tier.", result);
        Assert.DoesNotContain("defaults to", result);
    }

    // ── Required — other content preserved ────────────────────────────────────

    [Fact]
    public void RequiredParameter_PreservesRestOfDescription()
    {
        var input = "The OS disk type for the VM. Accepted values: Premium_LRS, Standard_LRS. Defaults to Standard_LRS.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: true);
        Assert.Contains("The OS disk type for the VM.", result);
        Assert.Contains("Accepted values: Premium_LRS, Standard_LRS.", result);
        Assert.DoesNotContain("Defaults to Standard_LRS", result);
    }

    [Fact]
    public void RequiredParameter_NoDefaultsLanguage_Unchanged()
    {
        var input = "The name of the Azure resource group.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: true);
        Assert.Equal(input, result);
    }

    [Fact]
    public void RequiredParameter_MultiSentence_StripsOnlyDefault()
    {
        var input = "The container image to deploy. Must be a valid Docker image reference. Defaults to mcr.microsoft.com/azuredocs/aci-helloworld.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: true);
        Assert.Contains("The container image to deploy.", result);
        Assert.Contains("Must be a valid Docker image reference.", result);
        Assert.DoesNotContain("Defaults to", result);
    }

    // ── Required — description is ONLY defaults sentence ──────────────────────

    [Fact]
    public void RequiredParameter_DescriptionIsOnlyDefaults_ReturnsEmpty()
    {
        var input = "Defaults to eastus.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: true);
        Assert.True(string.IsNullOrWhiteSpace(result) || !result.Contains("Defaults to"),
            $"Expected defaults sentence removed but got: '{result}'");
    }

    // ── Cleanup — no trailing whitespace or double periods ────────────────────

    [Fact]
    public void RequiredParameter_CleansUpTrailingWhitespace()
    {
        var input = "The Cosmos DB throughput value. Defaults to 400.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: true);
        Assert.Equal("The Cosmos DB throughput value.", result);
        Assert.DoesNotContain("  ", result);
        Assert.DoesNotContain("..", result);
    }

    [Fact]
    public void RequiredParameter_NoDoublePeriodsAfterStrip()
    {
        var input = "The region. Defaults to westus2.";
        var result = RequiredParameterDescriptionSanitizer.Apply(input, isRequired: true);
        Assert.DoesNotContain("..", result);
    }

    // ── Null / empty passthrough ───────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrEmptyInput_ReturnsAsIs(string? input)
    {
        var result = RequiredParameterDescriptionSanitizer.Apply(input!, isRequired: true);
        Assert.Equal(input, result);
    }
}
