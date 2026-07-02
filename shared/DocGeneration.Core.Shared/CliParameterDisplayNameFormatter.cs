// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Shared;

/// <summary>
/// Formats CLI switch names for documentation parameter table display cells.
/// </summary>
public static class CliParameterDisplayNameFormatter
{
    /// <summary>
    /// Strips one leading CLI switch prefix from a parameter name for display.
    /// Raw CLI names remain unchanged in source models for examples, matching, and validation.
    /// </summary>
    public static string StripCliPrefix(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        if (name.StartsWith("--", StringComparison.Ordinal))
        {
            return name[2..];
        }

        if (name.StartsWith("-", StringComparison.Ordinal))
        {
            return name[1..];
        }

        return name;
    }
}
