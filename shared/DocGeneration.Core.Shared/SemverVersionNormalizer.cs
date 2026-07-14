// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Shared;

/// <summary>
/// Normalizes semantic version strings for comparisons.
/// </summary>
public static class SemverVersionNormalizer
{
    /// <summary>
    /// Trims a version string and removes semantic-version build metadata.
    /// </summary>
    /// <param name="version">The version string to normalize.</param>
    /// <returns>The trimmed version without build metadata, or null when the input is null or whitespace.</returns>
    public static string? StripBuildMetadata(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var trimmed = version.Trim();
        var buildMetadataIndex = trimmed.IndexOf('+', StringComparison.Ordinal);
        return buildMetadataIndex >= 0
            ? trimmed[..buildMetadataIndex]
            : trimmed;
    }
}
