// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;

namespace Shared;

/// <summary>
/// Canonical sanitizer for CLI JSON payloads.
///
/// Some upstream MCP CLI builds emit raw, unescaped control characters
/// (for example <c>0x1A</c> SUB) inside JSON string values such as tool
/// descriptions. Those bytes are illegal per RFC 8259 and cause
/// <see cref="System.Text.Json.JsonException"/> when parsed. This helper
/// strips raw control characters that appear inside JSON string literals,
/// leaving structural whitespace and escape sequences untouched.
///
/// This is the single source of truth for control-character sanitization.
/// All CLI-JSON parse sites route through here rather than duplicating the
/// scanning logic.
/// </summary>
public static class JsonControlCharacterSanitizer
{
    /// <summary>
    /// Removes raw (unescaped) control characters that appear inside JSON
    /// string literals. Control characters outside of string literals
    /// (structural whitespace) are preserved, as are backslash escape
    /// sequences such as <c>\n</c> and <c>\"</c>.
    /// </summary>
    /// <param name="json">The raw JSON text, possibly containing stray control characters.</param>
    /// <returns>Sanitized JSON safe to pass to <c>System.Text.Json</c>.</returns>
    public static string StripInvalidControlCharacters(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return json;
        }

        var sb = new StringBuilder(json.Length);
        var inString = false;
        var escaping = false;

        foreach (var ch in json)
        {
            if (inString)
            {
                if (escaping)
                {
                    sb.Append(ch);
                    escaping = false;
                    continue;
                }

                if (ch == '\\')
                {
                    sb.Append(ch);
                    escaping = true;
                    continue;
                }

                if (ch == '"')
                {
                    sb.Append(ch);
                    inString = false;
                    continue;
                }

                if (char.IsControl(ch))
                {
                    continue;
                }

                sb.Append(ch);
                continue;
            }

            sb.Append(ch);
            if (ch == '"')
            {
                inString = true;
            }
        }

        return sb.ToString();
    }
}
