// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using NaturalLanguageGenerator;
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Tests for TextCleanup.NormalizeParameter to ensure parameter names
/// with type qualifiers (like "name") are fully preserved.
/// Priority: P0 — parameter name transformation affects all documentation.
/// Related PR: #40 — copilot/remove-name-type-parameter
/// </summary>
public class NormalizeParameterTests : IClassFixture<TextCleanupFixture>
{
    private readonly TextCleanupFixture _fixture;

    public NormalizeParameterTests(TextCleanupFixture fixture)
    {
        _fixture = fixture;
    }

    // ── Type Qualifier Preservation (Core Requirement) ──────────────

    [Theory]
    [InlineData("resource-group-name", "Resource group name")]
    [InlineData("resource-name", "Resource name")]
    [InlineData("secret-name", "Secret name")]
    [InlineData("storage-account-name", "Storage account name")]
    [InlineData("container-name", "Container name")]
    [InlineData("key-name", "Key name")]
    [InlineData("vault-name", "Vault name")]
    [InlineData("database-name", "Database name")]
    [InlineData("server-name", "Server name")]
    [InlineData("app-name", "App name")]
    public void NormalizeParameter_PreservesNameQualifier(string input, string expected)
    {
        // Act
        var result = TextCleanup.NormalizeParameter(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("resource-id", "Resource ID")]
    [InlineData("subscription-id", "Subscription ID")]
    [InlineData("tenant-id", "Tenant ID")]
    [InlineData("application-id", "Application ID")]
    [InlineData("object-id", "Object ID")]
    public void NormalizeParameter_PreservesIdQualifier(string input, string expected)
    {
        // Act
        var result = TextCleanup.NormalizeParameter(input);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("endpoint-uri", "Endpoint URI")]
    [InlineData("callback-uri", "Callback URI")]
    [InlineData("redirect-uri", "Redirect URI")]
    [InlineData("base-url", "Base URL")]
    [InlineData("source-url", "Source URL")]
    public void NormalizeParameter_PreservesUriAndUrlQualifiers(string input, string expected)
    {
        // Act
        var result = TextCleanup.NormalizeParameter(input);

        // Assert
        Assert.Equal(expected, result);
    }

    // ── Edge Cases ──────────────────────────────────────────────────

    [Fact]
    public void NormalizeParameter_SingleWordParameter_Capitalizes()
    {
        // Act
        var result = TextCleanup.NormalizeParameter("location");

        // Assert
        Assert.Equal("Location", result);
    }

    [Fact]
    public void NormalizeParameter_EmptyString_ReturnsUnknown()
    {
        // Act
        var result = TextCleanup.NormalizeParameter("");

        // Assert
        Assert.Equal("Unknown", result);
    }

    [Fact]
    public void NormalizeParameter_NullString_ReturnsUnknown()
    {
        // Act
        string? input = null;
        var result = TextCleanup.NormalizeParameter(input!);

        // Assert
        Assert.Equal("Unknown", result);
    }

    // ── CLI Style Parameters ────────────────────────────────────────

    [Theory]
    [InlineData("--resource-group-name", "Resource group name")]
    [InlineData("--secret-name", "Secret name")]
    [InlineData("--storage-account-name", "Storage account name")]
    [InlineData("--subscription-id", "Subscription ID")]
    public void NormalizeParameter_WithDoubleHyphens_StripsAndPreservesQualifiers(string input, string expected)
    {
        // Act
        var result = TextCleanup.NormalizeParameter(input);

        // Assert
        Assert.Equal(expected, result);
    }

    // ── Multiple Type Qualifiers ────────────────────────────────────

    [Theory]
    [InlineData("resource-group-name-id", "Resource group name ID")]
    [InlineData("storage-account-key-name", "Storage account key name")]
    [InlineData("vault-secret-name-uri", "Vault secret name URI")]
    public void NormalizeParameter_MultipleQualifiers_PreservesAll(string input, string expected)
    {
        // Act
        var result = TextCleanup.NormalizeParameter(input);

        // Assert
        Assert.Equal(expected, result);
    }

    // ── Acronym Handling ────────────────────────────────────────────

    [Theory]
    [InlineData("vm-name", "VM name")]
    [InlineData("api-key-name", "API key name")]
    [InlineData("sql-server-name", "SQL server name")]
    [InlineData("dns-name", "DNS name")]
    [InlineData("sku-name", "SKU name")]
    [InlineData("etag-value", "ETag value")]
    public void NormalizeParameter_WithAcronyms_PreservesAcronymsAndQualifiers(string input, string expected)
    {
        // Act
        var result = TextCleanup.NormalizeParameter(input);

        // Assert
        Assert.Equal(expected, result);
    }

    // ── Common Azure Resource Parameters ────────────────────────────

    [Theory]
    [InlineData("resource-group", "Resource group")]
    [InlineData("subscription", "Subscription")]
    [InlineData("location", "Location")]
    [InlineData("tag", "Tag")]
    [InlineData("sku", "SKU")]
    public void NormalizeParameter_CommonParameters_CorrectFormat(string input, string expected)
    {
        // Act
        var result = TextCleanup.NormalizeParameter(input);

        // Assert
        Assert.Equal(expected, result);
    }

    // ── Bug Regression Tests ────────────────────────────────────────

    [Fact]
    public void NormalizeParameter_ResourceGroupName_NotTruncatedToResourceGroup()
    {
        // This test ensures we DON'T strip "name" suffix
        // Bug scenario: "resource-group-name" → "Resource group" (WRONG)
        // Expected: "resource-group-name" → "Resource group name" (CORRECT)

        // Act
        var result = TextCleanup.NormalizeParameter("resource-group-name");

        // Assert
        Assert.NotEqual("Resource group", result);
        Assert.Equal("Resource group name", result);
    }

    [Fact]
    public void NormalizeParameter_SecretName_NotTruncatedToSecret()
    {
        // Act
        var result = TextCleanup.NormalizeParameter("secret-name");

        // Assert
        Assert.NotEqual("Secret", result);
        Assert.Equal("Secret name", result);
    }

    [Fact]
    public void NormalizeParameter_StorageAccountName_NotTruncatedToStorageAccount()
    {
        // Act
        var result = TextCleanup.NormalizeParameter("storage-account-name");

        // Assert
        Assert.NotEqual("Storage account", result);
        Assert.Equal("Storage account name", result);
    }

    // ── Word Order and Spacing ──────────────────────────────────────

    [Theory]
    [InlineData("a-b-c", "A b c")]
    [InlineData("one-two-three-four", "One two three four")]
    [InlineData("very-long-parameter-name-with-many-parts", "Very long parameter name with many parts")]
    public void NormalizeParameter_PreservesAllWords_CorrectSpacing(string input, string expected)
    {
        // Act
        var result = TextCleanup.NormalizeParameter(input);

        // Assert
        Assert.Equal(expected, result);
    }

    // ── Special Characters and Formats ──────────────────────────────

    [Theory]
    [InlineData("resource_group_name", "Resource_group_name")] // Underscores not split
    [InlineData("resourceGroupName", "ResourceGroupName")] // CamelCase is preserved when no hyphens are present
    public void NormalizeParameter_NonHyphenSeparators_NotSplit(string input, string expected)
    {
        // Act
        var result = TextCleanup.NormalizeParameter(input);

        // Assert
        Assert.Equal(expected, result);
    }

    // ── Case Sensitivity ────────────────────────────────────────────

    [Theory]
    [InlineData("RESOURCE-GROUP-NAME", "RESOURCE group name")] // First word preserves ALL CAPS
    [InlineData("Resource-Group-Name", "Resource group name")]
    [InlineData("resource-GROUP-name", "Resource group name")] 
    public void NormalizeParameter_MixedCase_NormalizesCorrectly(string input, string expected)
    {
        // Act
        var result = TextCleanup.NormalizeParameter(input);

        // Assert
        Assert.Equal(expected, result);
    }

    // ── Real-World Azure Parameters ─────────────────────────────────

    [Theory]
    [InlineData("workspace-name", "Workspace name")]
    [InlineData("cluster-name", "Cluster name")]
    [InlineData("namespace-name", "Namespace name")]
    [InlineData("topic-name", "Topic name")]
    [InlineData("queue-name", "Queue name")]
    [InlineData("hub-name", "Hub name")]
    [InlineData("registry-name", "Registry name")]
    [InlineData("account-name", "Account name")]
    [InlineData("pool-name", "Pool name")]
    [InlineData("profile-name", "Profile name")]
    public void NormalizeParameter_AzureServiceParameters_PreservesNameQualifier(string input, string expected)
    {
        // Act
        var result = TextCleanup.NormalizeParameter(input);

        // Assert
        Assert.Equal(expected, result);
    }

    // ── Consistency Across Similar Patterns ────────────────────────

    [Theory]
    [InlineData("name", "Name")]
    [InlineData("id", "ID")]
    [InlineData("uri", "URI")]
    [InlineData("url", "URL")]
    [InlineData("etag", "ETag")]
    [InlineData("type", "Type")]
    [InlineData("status", "Status")]
    public void NormalizeParameter_SingleWordQualifiers_FormattedCorrectly(string input, string expected)
    {
        // Act
        var result = TextCleanup.NormalizeParameter(input);

        // Assert
        Assert.Equal(expected, result);
    }
}
