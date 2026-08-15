// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Shared alias derivation logic used by the emitter. Ensures alias computation
/// exists in exactly one place (shared by emitter and any future consumer).
/// </summary>
public static class CanonicalAliasDeriver
{
    /// <summary>
    /// Derives display aliases per AD-030 §1.3.
    /// </summary>
    public static string[] DeriveDisplayAliases(string canonicalName, string displayName)
    {
        var aliases = new HashSet<string>(StringComparer.Ordinal);

        // Normalize(displayName)
        var normalizedDisplay = CanonicalParameterNormalizer.Normalize(displayName);
        if (!string.IsNullOrEmpty(normalizedDisplay))
            aliases.Add(normalizedDisplay);

        // Normalize(canonicalName) — identity itself
        var normalizedCanonical = CanonicalParameterNormalizer.Normalize(canonicalName);
        if (!string.IsNullOrEmpty(normalizedCanonical))
            aliases.Add(normalizedCanonical);

        // SpaceJoin(Split(canonicalName, '-')) = same as just joining with space then normalizing
        var spaceJoined = string.Join(" ", canonicalName.Split('-', StringSplitOptions.RemoveEmptyEntries));
        var normalizedSpace = CanonicalParameterNormalizer.Normalize(spaceJoined);
        if (!string.IsNullOrEmpty(normalizedSpace))
            aliases.Add(normalizedSpace);

        return aliases.ToArray();
    }

    /// <summary>
    /// Derives placeholder aliases per AD-030 §1.3.
    /// </summary>
    public static string[] DerivePlaceholderAliases(string canonicalName, string displayName)
    {
        var aliases = new HashSet<string>(StringComparer.Ordinal);

        // canonicalName itself
        if (!string.IsNullOrEmpty(canonicalName))
            aliases.Add(canonicalName);

        // Replace(canonicalName, '-', '_')
        var underscored = canonicalName.Replace('-', '_');
        if (!string.IsNullOrEmpty(underscored))
            aliases.Add(underscored);

        // Normalize(displayName)
        var normalizedDisplay = CanonicalParameterNormalizer.Normalize(displayName);
        if (!string.IsNullOrEmpty(normalizedDisplay))
            aliases.Add(normalizedDisplay);

        // Replace(Normalize(displayName), '-', '_')
        var displayUnderscored = normalizedDisplay.Replace('-', '_');
        if (!string.IsNullOrEmpty(displayUnderscored))
            aliases.Add(displayUnderscored);

        return aliases.ToArray();
    }
}
