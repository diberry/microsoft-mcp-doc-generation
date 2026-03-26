// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace ExamplePromptGeneratorStandalone.Sanitizers;

/// <summary>
/// Post-processing guard that detects and replaces hardcoded credentials
/// (passwords, API keys, tokens, connection string secrets) in generated
/// example prompts. Defense-in-depth for issue #264.
///
/// STUB — implementation pending (TDD red phase).
/// </summary>
public static class CredentialSanitizer
{
    /// <summary>
    /// Scans <paramref name="prompt"/> for credential patterns and replaces
    /// them with safe placeholders. Returns the sanitized string.
    /// </summary>
    public static string Sanitize(string prompt)
    {
        // TODO: Morgan — implement pattern matching and replacement (#264)
        return prompt;
    }
}
