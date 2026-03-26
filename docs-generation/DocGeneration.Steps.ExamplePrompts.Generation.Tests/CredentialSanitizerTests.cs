// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ExamplePromptGeneratorStandalone.Sanitizers;
using Xunit;

namespace ExamplePromptGeneratorStandalone.Tests;

/// <summary>
/// TDD red-phase tests for CredentialSanitizer (issue #264).
/// These tests define the expected behavior; they FAIL until
/// Morgan implements the sanitizer logic.
/// </summary>
public class CredentialSanitizerTests
{
    // ── Password patterns ───────────────────────────────────────────

    [Theory]
    [InlineData("Create a secret with value 'P@ssw0rd!2026' in key vault 'prod-kv'.")]
    [InlineData("Set the config value to 'S3cureP@ss!' for app service 'my-app'.")]
    [InlineData("Update password to 'Admin#123!' on server 'prod-sql-server'.")]
    public void DetectsPasswordPatterns_SpecialCharPasswords(string prompt)
    {
        var result = CredentialSanitizer.Sanitize(prompt);

        // The hardcoded password value must be gone
        Assert.DoesNotMatch(@"P@ssw0rd!2026", result);
        Assert.DoesNotMatch(@"S3cureP@ss!", result);
        Assert.DoesNotMatch(@"Admin#123!", result);

        // Structural parts of the prompt must survive
        Assert.Contains("key vault", result, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Password=secret")]
    [InlineData("password=MyP@ss123")]
    [InlineData("Password=P@ssw0rd!2026")]
    public void DetectsPasswordPatterns_KeyValueFormat(string fragment)
    {
        var prompt = $"Connect using '{fragment}' on server 'prod-sql-server'.";

        var result = CredentialSanitizer.Sanitize(prompt);

        // The password value after the = sign must be replaced
        Assert.DoesNotContain("=secret", result);
        Assert.DoesNotContain("=MyP@ss123", result);
        Assert.DoesNotContain("=P@ssw0rd!2026", result);
    }

    // ── API key patterns ────────────────────────────────────────────

    [Theory]
    [InlineData("sk_live_4f3b2a")]
    [InlineData("sk_test_7x9m2k")]
    [InlineData("pg_live_98zxy")]
    public void DetectsApiKeyPatterns_PrefixedKeys(string key)
    {
        var prompt = $"Store API key '{key}' in key vault 'finance-kv'.";

        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain(key, result);
        // Key vault reference must survive
        Assert.Contains("finance-kv", result);
    }

    [Theory]
    [InlineData("api_key=abc123secret")]
    [InlineData("apiKey=xK9mW2pL7qR")]
    public void DetectsApiKeyPatterns_KeyValueAssignment(string fragment)
    {
        var prompt = $"Configure Cosmos DB with '{fragment}' on account 'companydata2024'.";

        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain(fragment, result);
        Assert.Contains("companydata2024", result);
    }

    // ── Bearer / JWT token patterns ─────────────────────────────────

    [Theory]
    [InlineData("Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9")]
    [InlineData("eyJhbGciOi")]
    public void DetectsTokenPatterns_BearerAndJwt(string token)
    {
        var prompt = $"Authenticate to App Service 'my-webapp' using '{token}'.";

        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain("eyJhbGci", result);
        Assert.Contains("my-webapp", result);
    }

    // ── Connection string secrets ───────────────────────────────────

    [Fact]
    public void DetectsConnectionStringSecrets_SqlServer()
    {
        var prompt =
            "Connect to SQL database using 'Server=myserver.database.windows.net;" +
            "User ID=admin;Password=P@ssw0rd!2026' in resource group 'rg-prod'.";

        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain("P@ssw0rd!2026", result);
        // Non-secret connection parts should survive OR be replaced with safe version
        Assert.Contains("rg-prod", result);
    }

    [Fact]
    public void DetectsConnectionStringSecrets_PostgreSQL()
    {
        var prompt =
            "Set connection string to 'Host=dev-pg-server.postgres.database.azure.com;" +
            "Password=pg_live_98zxy;Database=analytics-db' for app service 'my-api'.";

        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain("pg_live_98zxy", result);
        Assert.Contains("my-api", result);
    }

    // ── Preserves non-secret values ─────────────────────────────────

    [Theory]
    [InlineData("List all accounts in resource group 'rg-prod'.")]
    [InlineData("Get key vault 'prod-kv' in subscription 'my-subscription'.")]
    [InlineData("Create a container 'backups' in storage account 'mystorageacct'.")]
    [InlineData("Delete database 'analytics-db' on server 'prod-sql-server'.")]
    [InlineData("Show me the Cosmos DB account 'companydata2024' in location 'eastus'.")]
    public void PreservesNonSecretValues_NoModification(string prompt)
    {
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.Equal(prompt, result);
    }

    [Theory]
    [InlineData("name: 'my-database'")]
    [InlineData("resource-group: 'my-rg'")]
    [InlineData("location: 'eastus'")]
    [InlineData("subscription: 'contoso-sub'")]
    [InlineData("account: 'mystorageacct'")]
    public void PreservesNonSecretValues_CommonParamFormats(string fragment)
    {
        var prompt = $"Update the config with {fragment} in App Service 'my-app'.";

        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.Equal(prompt, result);
    }

    // ── Multiple secrets in one prompt ──────────────────────────────

    [Fact]
    public void HandlesMultipleSecretsInOnePrompt()
    {
        var prompt =
            "Create a secret 'api-key' with value 'sk_live_4f3b2a' " +
            "and a secret 'db-password' with value 'P@ssw0rd!2026' " +
            "in key vault 'prod-kv'.";

        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain("sk_live_4f3b2a", result);
        Assert.DoesNotContain("P@ssw0rd!2026", result);
        // Structural parts survive
        Assert.Contains("prod-kv", result);
        Assert.Contains("api-key", result);
        Assert.Contains("db-password", result);
    }

    [Fact]
    public void HandlesMultipleSecretsInOnePrompt_MixedTypes()
    {
        var prompt =
            "Store token 'eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkw' " +
            "and API key 'pg_live_98zxy' in key vault 'backup-kv'.";

        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain("eyJhbGci", result);
        Assert.DoesNotContain("pg_live_98zxy", result);
        Assert.Contains("backup-kv", result);
    }

    // ── Real-world issue #264 reproduction ──────────────────────────

    [Fact]
    public void SqlNamespaceRealWorldExample_Issue264()
    {
        // Exact pattern from the bug report: SQL tool prompt with password in connection string
        var prompt =
            "Create a firewall rule on server 'prod-sql-server' with " +
            "value 'Server=myserver.database.windows.net;User ID=admin;Password=P@ssw0rd!2026'.";

        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain("P@ssw0rd!2026", result);
        Assert.Contains("prod-sql-server", result);
    }

    [Fact]
    public void SqlNamespaceRealWorldExample_ValueBankCredentials()
    {
        // The exact values from DeterministicExamplePromptGenerator.ValueBank["value"]
        var dangerousValues = new[]
        {
            "P@ssw0rd!2026",
            "sk_live_4f3b2a",
            "eyJhbGciOi",
            "pg_live_98zxy"
        };

        foreach (var dangerous in dangerousValues)
        {
            var prompt = $"Create a config setting with value '{dangerous}' in App Configuration 'my-appconfig'.";

            var result = CredentialSanitizer.Sanitize(prompt);

            Assert.DoesNotContain(dangerous, result,
                StringComparison.Ordinal);
        }
    }

    // ── Edge cases ──────────────────────────────────────────────────

    [Fact]
    public void EmptyString_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, CredentialSanitizer.Sanitize(string.Empty));
    }

    [Fact]
    public void NullInput_DoesNotThrow()
    {
        // Sanitizer should handle null gracefully (return empty or null)
        var result = CredentialSanitizer.Sanitize(null!);
        Assert.True(result == null || result == string.Empty);
    }

    [Fact]
    public void DefaultEndpointsProtocol_IsNotACredential()
    {
        // "DefaultEndpointsProtocol=https" is in the ValueBank but is NOT a secret
        var prompt = "Set endpoint protocol to 'DefaultEndpointsProtocol=https' for storage account 'mystorageacct'.";

        var result = CredentialSanitizer.Sanitize(prompt);

        // This is a protocol prefix, not a credential — should be preserved
        Assert.Contains("DefaultEndpointsProtocol=https", result);
    }
}
