// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;

namespace ExamplePromptGeneratorStandalone.Sanitizers;

/// <summary>
/// Post-processing guard that detects and replaces hardcoded credentials
/// (passwords, API keys, tokens, connection string secrets) in generated
/// example prompts. Defense-in-depth for issue #264.
/// </summary>
public static partial class CredentialSanitizer
{
    // Password=value, password=value, pwd=value (but NOT DefaultEndpointsProtocol= etc.)
    [GeneratedRegex(@"\b(?:password|pwd)\s*=\s*[^;'""\s]+", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordKeyValueRegex();

    // api_key=value, apiKey=value
    [GeneratedRegex(@"\bapi[_]?key\s*=\s*[^\s;'""]+", RegexOptions.IgnoreCase)]
    private static partial Regex ApiKeyAssignmentRegex();

    // sk_live_*, sk_test_*, pg_live_*, pg_test_*
    [GeneratedRegex(@"\b(?:sk|pg)_(?:live|test)_[A-Za-z0-9]+")]
    private static partial Regex PrefixedApiKeyRegex();

    // Bearer eyJ...
    [GeneratedRegex(@"Bearer\s+eyJ[A-Za-z0-9+/=._-]+")]
    private static partial Regex BearerTokenRegex();

    // Standalone JWT tokens: eyJ...
    [GeneratedRegex(@"eyJ[A-Za-z0-9+/=._-]+")]
    private static partial Regex JwtTokenRegex();

    // Single-quoted values containing @, #, or ! (password-like)
    [GeneratedRegex(@"'([^']*[@#!][^']*)'")]
    private static partial Regex QuotedPasswordLikeRegex();

    [GeneratedRegex(@"[A-Za-z]")]
    private static partial Regex HasLettersRegex();

    // ── Azure endpoint patterns (issue #275) ────────────────────────
    // Each regex captures any hostname prefix before the Azure service suffix.
    // Hosts already containing fictional domain names are excluded via a
    // negative lookahead so we don't double-replace safe values.

    private static readonly string[] FictionalMarkers = ["contoso", "fabrikam", "adventure-works", "example", "fictional"];

    [GeneratedRegex(@"[A-Za-z0-9][\w.-]*\.database\.windows\.net")]
    private static partial Regex SqlEndpointRegex();

    [GeneratedRegex(@"[A-Za-z0-9][\w.-]*\.blob\.core\.windows\.net")]
    private static partial Regex BlobEndpointRegex();

    [GeneratedRegex(@"[A-Za-z0-9][\w.-]*\.table\.core\.windows\.net")]
    private static partial Regex TableEndpointRegex();

    [GeneratedRegex(@"[A-Za-z0-9][\w.-]*\.queue\.core\.windows\.net")]
    private static partial Regex QueueEndpointRegex();

    [GeneratedRegex(@"[A-Za-z0-9][\w.-]*\.vault\.azure\.net")]
    private static partial Regex KeyVaultEndpointRegex();

    [GeneratedRegex(@"[A-Za-z0-9][\w.-]*\.azurewebsites\.net")]
    private static partial Regex WebAppEndpointRegex();

    [GeneratedRegex(@"[A-Za-z0-9][\w.-]*\.redis\.cache\.windows\.net")]
    private static partial Regex RedisEndpointRegex();

    [GeneratedRegex(@"[A-Za-z0-9][\w.-]*\.servicebus\.windows\.net")]
    private static partial Regex ServiceBusEndpointRegex();

    [GeneratedRegex(@"[A-Za-z0-9][\w.-]*\.documents\.azure\.com")]
    private static partial Regex CosmosEndpointRegex();

    /// <summary>
    /// Endpoint patterns and their safe replacements for Azure service hostnames.
    /// </summary>
    private static readonly (Func<Regex> Pattern, string Replacement)[] EndpointRules =
    [
        (() => SqlEndpointRegex(),        "contoso-sql.database.windows.net"),
        (() => BlobEndpointRegex(),       "contoso.blob.core.windows.net"),
        (() => TableEndpointRegex(),      "contoso.table.core.windows.net"),
        (() => QueueEndpointRegex(),      "contoso.queue.core.windows.net"),
        (() => KeyVaultEndpointRegex(),   "contoso-kv.vault.azure.net"),
        (() => WebAppEndpointRegex(),     "contoso-app.azurewebsites.net"),
        (() => RedisEndpointRegex(),      "contoso-cache.redis.cache.windows.net"),
        (() => ServiceBusEndpointRegex(), "contoso-bus.servicebus.windows.net"),
        (() => CosmosEndpointRegex(),     "contoso-cosmos.documents.azure.com"),
    ];

    /// <summary>
    /// Scans <paramref name="prompt"/> for credential patterns and replaces
    /// them with safe placeholders. Returns the sanitized string.
    /// </summary>
    public static string Sanitize(string prompt)
    {
        if (string.IsNullOrEmpty(prompt))
            return prompt;

        // 1. Password=value / pwd=value — replace only the secret value
        prompt = PasswordKeyValueRegex().Replace(prompt, match =>
        {
            var eqIndex = match.Value.IndexOf('=');
            return match.Value[..(eqIndex + 1)] + "<secure-password>";
        });

        // 2. api_key=value / apiKey=value — replace the entire assignment
        prompt = ApiKeyAssignmentRegex().Replace(prompt, "<api-key>");

        // 3. Prefixed API keys (sk_live_*, pg_live_*, etc.)
        prompt = PrefixedApiKeyRegex().Replace(prompt, "<api-key>");

        // 4. Bearer tokens
        prompt = BearerTokenRegex().Replace(prompt, "Bearer <token>");

        // 5. Standalone JWT tokens (eyJ...)
        prompt = JwtTokenRegex().Replace(prompt, "<token>");

        // 6. Quoted values with special chars (@, #, !) — catch remaining passwords
        prompt = QuotedPasswordLikeRegex().Replace(prompt, match =>
        {
            var value = match.Groups[1].Value;
            if (HasLettersRegex().IsMatch(value))
                return "'<secure-password>'";
            return match.Value;
        });

        // 7. Azure service endpoints — replace realistic hostnames with contoso (issue #275)
        foreach (var (pattern, replacement) in EndpointRules)
        {
            prompt = pattern().Replace(prompt, match =>
            {
                // Skip endpoints that already use fictional domain names
                var host = match.Value;
                foreach (var marker in FictionalMarkers)
                {
                    if (host.Contains(marker, StringComparison.OrdinalIgnoreCase))
                        return host;
                }
                return replacement;
            });
        }

        return prompt;
    }
}
