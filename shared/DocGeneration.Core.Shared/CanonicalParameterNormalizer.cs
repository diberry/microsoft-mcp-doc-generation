// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.RegularExpressions;

namespace Shared;

/// <summary>
/// Pure, static, culture-invariant normalization of parameter names to canonical form.
/// AD-030 §4.1.
/// </summary>
public static class CanonicalParameterNormalizer
{
    private static readonly Regex NonAlphaNumHyphen = new(@"[^a-z0-9\-]+", RegexOptions.Compiled);
    private static readonly Regex ConsecutiveHyphens = new(@"-{2,}", RegexOptions.Compiled);

    /// <summary>
    /// Normalizes a parameter name to canonical form:
    /// 1. Strip leading "--"
    /// 2. ToLowerInvariant
    /// 3. Replace '_' and ' ' with '-'
    /// 4. Remove chars not in [a-z0-9-]
    /// 5. Collapse consecutive hyphens
    /// 6. Trim leading/trailing hyphens
    /// </summary>
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var s = input.Trim();

        // Strip leading "--"
        if (s.StartsWith("--", StringComparison.Ordinal))
            s = s[2..];

        // Lowercase invariant
        s = s.ToLower(CultureInfo.InvariantCulture);

        // Replace underscores and spaces with hyphens
        s = s.Replace('_', '-').Replace(' ', '-');

        // Remove non [a-z0-9-]
        s = NonAlphaNumHyphen.Replace(s, "-");

        // Collapse consecutive hyphens
        s = ConsecutiveHyphens.Replace(s, "-");

        // Trim leading/trailing hyphens
        s = s.Trim('-');

        return s;
    }
}
