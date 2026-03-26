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

        return prompt;
    }
}
