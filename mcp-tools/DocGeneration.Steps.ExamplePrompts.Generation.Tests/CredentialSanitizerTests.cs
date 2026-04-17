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

    // ── Azure endpoint sanitization (issue #275) ────────────────────

    [Theory]
    [InlineData("sql-prod.database.windows.net", "contoso-sql.database.windows.net")]
    [InlineData("sql-inventory.database.windows.net", "contoso-sql.database.windows.net")]
    [InlineData("orders-sql.database.windows.net", "contoso-sql.database.windows.net")]
    [InlineData("myserver.database.windows.net", "contoso-sql.database.windows.net")]
    public void SanitizesEndpoints_SqlDatabase(string endpoint, string expected)
    {
        var prompt = $"Connect to '{endpoint}' and run the query.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain(endpoint, result);
        Assert.Contains(expected, result);
    }

    [Theory]
    [InlineData("mystorage.blob.core.windows.net", "contoso.blob.core.windows.net")]
    [InlineData("data-lake.blob.core.windows.net", "contoso.blob.core.windows.net")]
    public void SanitizesEndpoints_BlobStorage(string endpoint, string expected)
    {
        var prompt = $"Upload file to '{endpoint}/container/blob.txt'.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain(endpoint, result);
        Assert.Contains(expected, result);
    }

    [Theory]
    [InlineData("mystorage.table.core.windows.net", "contoso.table.core.windows.net")]
    [InlineData("mystorage.queue.core.windows.net", "contoso.queue.core.windows.net")]
    public void SanitizesEndpoints_TableAndQueue(string endpoint, string expected)
    {
        var prompt = $"Query data from '{endpoint}'.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain(endpoint, result);
        Assert.Contains(expected, result);
    }

    [Theory]
    [InlineData("prod-kv.vault.azure.net", "contoso-kv.vault.azure.net")]
    [InlineData("finance-kv.vault.azure.net", "contoso-kv.vault.azure.net")]
    public void SanitizesEndpoints_KeyVault(string endpoint, string expected)
    {
        var prompt = $"Get secret from '{endpoint}/secrets/my-secret'.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain(endpoint, result);
        Assert.Contains(expected, result);
    }

    [Theory]
    [InlineData("my-webapp.azurewebsites.net", "contoso-app.azurewebsites.net")]
    [InlineData("api-prod.azurewebsites.net", "contoso-app.azurewebsites.net")]
    public void SanitizesEndpoints_WebApps(string endpoint, string expected)
    {
        var prompt = $"Deploy code to '{endpoint}'.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain(endpoint, result);
        Assert.Contains(expected, result);
    }

    [Theory]
    [InlineData("my-cache.redis.cache.windows.net", "contoso-cache.redis.cache.windows.net")]
    public void SanitizesEndpoints_Redis(string endpoint, string expected)
    {
        var prompt = $"Connect to Redis cache at '{endpoint}'.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain(endpoint, result);
        Assert.Contains(expected, result);
    }

    [Theory]
    [InlineData("my-bus.servicebus.windows.net", "contoso-bus.servicebus.windows.net")]
    public void SanitizesEndpoints_ServiceBus(string endpoint, string expected)
    {
        var prompt = $"Send message to '{endpoint}/my-queue'.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain(endpoint, result);
        Assert.Contains(expected, result);
    }

    [Theory]
    [InlineData("my-cosmos.documents.azure.com", "contoso-cosmos.documents.azure.com")]
    public void SanitizesEndpoints_CosmosDb(string endpoint, string expected)
    {
        var prompt = $"Query items from '{endpoint}/dbs/mydb'.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain(endpoint, result);
        Assert.Contains(expected, result);
    }

    [Theory]
    [InlineData("contoso-sql.database.windows.net")]
    [InlineData("contoso.blob.core.windows.net")]
    [InlineData("fabrikam-kv.vault.azure.net")]
    [InlineData("adventure-works-app.azurewebsites.net")]
    [InlineData("example-bus.servicebus.windows.net")]
    [InlineData("fictional-cache.redis.cache.windows.net")]
    public void PreservesFictionalEndpoints_NoModification(string endpoint)
    {
        var prompt = $"Connect to '{endpoint}' for testing.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.Contains(endpoint, result);
    }

    [Fact]
    public void SanitizesEndpoints_PreservesPathAfterHostname()
    {
        var prompt = "Connect to 'myserver.database.windows.net/mydb' with user 'admin'.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.Contains("contoso-sql.database.windows.net/mydb", result);
        Assert.DoesNotContain("myserver.database.windows.net", result);
    }

    [Fact]
    public void SanitizesEndpoints_MixedWithCredentials()
    {
        var prompt =
            "Connect to 'Server=myserver.database.windows.net;User ID=admin;Password=P@ssw0rd!2026' " +
            "and upload to 'data-lake.blob.core.windows.net/backups'.";

        var result = CredentialSanitizer.Sanitize(prompt);

        // Credentials sanitized
        Assert.DoesNotContain("P@ssw0rd!2026", result);
        // Endpoints sanitized
        Assert.DoesNotContain("myserver.database.windows.net", result);
        Assert.Contains("contoso-sql.database.windows.net", result);
        Assert.DoesNotContain("data-lake.blob.core.windows.net", result);
        Assert.Contains("contoso.blob.core.windows.net", result);
    }

    [Fact]
    public void SanitizesEndpoints_MultipleEndpointsInOnePrompt()
    {
        var prompt =
            "Copy data from 'source-sql.database.windows.net' " +
            "to 'backup-storage.blob.core.windows.net/archive'.";

        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.DoesNotContain("source-sql.database.windows.net", result);
        Assert.DoesNotContain("backup-storage.blob.core.windows.net", result);
        Assert.Contains("contoso-sql.database.windows.net", result);
        Assert.Contains("contoso.blob.core.windows.net/archive", result);
    }

    [Fact]
    public void SanitizesEndpoints_Issue275_AppServiceRealWorld()
    {
        // Exact patterns from the issue report
        var endpoints = new[]
        {
            "sql-prod.database.windows.net",
            "sql-inventory.database.windows.net",
            "orders-sql.database.windows.net",
            "myserver.database.windows.net",
        };

        foreach (var endpoint in endpoints)
        {
            var prompt = $"Query the database at '{endpoint}'.";
            var result = CredentialSanitizer.Sanitize(prompt);

            Assert.DoesNotContain(endpoint, result);
            Assert.Contains("contoso-sql.database.windows.net", result);
        }
    }

    // ── Issue #288: sanitizer applied to prompt list (integration) ───

    [Fact]
    public void Issue288_SanitizeAppliedToPromptList_ReplacesAllEndpoints()
    {
        // Simulates the AI-generated prompt list that Program.cs sanitizes
        // before writing to disk (issue #288 integration pattern).
        var prompts = new List<string>
        {
            "List all databases on server 'sql-prod.database.windows.net'.",
            "Get logs from 'my-webapp.azurewebsites.net' in resource group 'rg-prod'.",
            "Retrieve secrets from vault 'finance-kv.vault.azure.net/secrets/db-pass'.",
            "Upload backup to 'data-lake.blob.core.windows.net/backups'.",
            "Send message to queue 'my-bus.servicebus.windows.net/my-queue'.",
        };

        var sanitized = prompts.Select(CredentialSanitizer.Sanitize).ToList();

        // Every realistic endpoint must be replaced
        Assert.DoesNotContain(sanitized, p => p.Contains("sql-prod.database.windows.net"));
        Assert.DoesNotContain(sanitized, p => p.Contains("my-webapp.azurewebsites.net"));
        Assert.DoesNotContain(sanitized, p => p.Contains("finance-kv.vault.azure.net"));
        Assert.DoesNotContain(sanitized, p => p.Contains("data-lake.blob.core.windows.net"));
        Assert.DoesNotContain(sanitized, p => p.Contains("my-bus.servicebus.windows.net"));

        // Safe replacements must be present
        Assert.Contains(sanitized, p => p.Contains("contoso-sql.database.windows.net"));
        Assert.Contains(sanitized, p => p.Contains("contoso-app.azurewebsites.net"));
        Assert.Contains(sanitized, p => p.Contains("contoso-kv.vault.azure.net"));
        Assert.Contains(sanitized, p => p.Contains("contoso.blob.core.windows.net"));
        Assert.Contains(sanitized, p => p.Contains("contoso-bus.servicebus.windows.net"));

        // Non-endpoint content must survive
        Assert.Contains(sanitized, p => p.Contains("rg-prod"));
        Assert.Contains(sanitized, p => p.Contains("backups"));
    }

    [Fact]
    public void Issue288_SanitizeIsIdempotent_DeterministicPromptsUnchanged()
    {
        // Deterministic prompts already sanitize internally.
        // Running sanitizer again (defense-in-depth in Program.cs) must be a no-op.
        var alreadySanitized = new[]
        {
            "List all databases on server 'contoso-sql.database.windows.net'.",
            "Get secrets from 'contoso-kv.vault.azure.net/secrets/my-secret'.",
            "Deploy to 'contoso-app.azurewebsites.net' in resource group 'rg-prod'.",
        };

        foreach (var prompt in alreadySanitized)
        {
            var result = CredentialSanitizer.Sanitize(prompt);
            Assert.Equal(prompt, result);
        }
    }

    // ── Issue #300: Backslash-escaped angle-bracket placeholders ─────

    [Fact]
    public void Issue300_PasswordPlaceholder_UsesEscapedAngleBrackets()
    {
        var prompt = "Connect with 'Server=myserver.database.windows.net;Password=P@ssw0rd!2026'.";
        var result = CredentialSanitizer.Sanitize(prompt);

        // Must use backslash-escaped angle brackets for MS Learn compatibility
        Assert.Contains(@"\<secure-password\>", result);
        // Must NOT contain unescaped angle brackets
        Assert.DoesNotContain("<secure-password>", result);
    }

    [Fact]
    public void Issue300_ApiKeyPlaceholder_UsesEscapedAngleBrackets()
    {
        var prompt = "Set api_key=abc123secret for Cosmos DB.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.Contains(@"\<api-key\>", result);
        Assert.DoesNotContain("<api-key>", result);
    }

    [Fact]
    public void Issue300_PrefixedApiKeyPlaceholder_UsesEscapedAngleBrackets()
    {
        var prompt = "Store key 'sk_live_4f3b2a' in vault.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.Contains(@"\<api-key\>", result);
        Assert.DoesNotContain("<api-key>", result);
    }

    [Fact]
    public void Issue300_BearerTokenPlaceholder_UsesEscapedAngleBrackets()
    {
        var prompt = "Auth with 'Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9'.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.Contains(@"Bearer \<token\>", result);
        Assert.DoesNotContain("Bearer <token>", result);
    }

    [Fact]
    public void Issue300_JwtTokenPlaceholder_UsesEscapedAngleBrackets()
    {
        var prompt = "Use token 'eyJhbGciOi' for auth.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.Contains(@"\<token\>", result);
        Assert.DoesNotContain("<token>", result);
    }

    [Fact]
    public void Issue300_QuotedPasswordPlaceholder_UsesEscapedAngleBrackets()
    {
        var prompt = "Create secret with value 'P@ssw0rd!2026' in vault.";
        var result = CredentialSanitizer.Sanitize(prompt);

        Assert.Contains(@"\<secure-password\>", result);
        Assert.DoesNotContain("<secure-password>", result);
    }

    [Fact]
    public void Issue300_NoUnescapedAngleBracketPlaceholders_AnyCredentialType()
    {
        // Comprehensive test: ALL credential patterns must produce escaped output
        var prompts = new[]
        {
            "Password=MySecret123",
            "api_key=xK9mW2pL7qR",
            "sk_test_7x9m2k",
            "Bearer eyJhbGciOiJIUzI1NiJ9.eyJzdWI",
            "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9",
            "Set value 'Admin#123!' for config.",
        };

        foreach (var prompt in prompts)
        {
            var result = CredentialSanitizer.Sanitize(prompt);

            // No unescaped angle-bracket placeholders (would fail MS Learn validation)
            Assert.DoesNotMatch(@"(?<!\\)<(secure-password|api-key|token)>", result);
        }
    }

    [Fact]
    public void Issue300_EscapedPlaceholders_AreIdempotent()
    {
        // Already-escaped placeholders must pass through unchanged
        var alreadyEscaped = new[]
        {
            @"Connect with Password=\<secure-password\> on server.",
            @"Set \<api-key\> for Cosmos DB.",
            @"Auth with Bearer \<token\> for the API.",
        };

        foreach (var prompt in alreadyEscaped)
        {
            var result = CredentialSanitizer.Sanitize(prompt);
            Assert.Equal(prompt, result);
        }
    }
}
