// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Threading;

namespace Shared;

/// <summary>
/// Resolves shared tokens in prompt files. Replaces {{ACROLINX_RULES}} with the
/// canonical Acrolinx compliance rules from data/shared-acrolinx-rules.txt.
/// This ensures a single source of truth for style rules across all pipeline steps.
/// </summary>
public static class PromptTokenResolver
{
    private const string AcrolinxToken = "{{ACROLINX_RULES}}";
    private static string? _acrolinxRules;
    private static readonly Lock _lock = new();

    /// <summary>
    /// Resolves all known tokens in a prompt string.
    /// </summary>
    /// <param name="prompt">The raw prompt text, possibly containing {{ACROLINX_RULES}} tokens.</param>
    /// <param name="dataDir">Path to the data directory containing shared-acrolinx-rules.txt.</param>
    /// <returns>The prompt with all tokens replaced by their content.</returns>
    public static string Resolve(string prompt, string dataDir)
    {
        if (!prompt.Contains(AcrolinxToken))
            return prompt;

        lock (_lock)
        {
            _acrolinxRules ??= File.ReadAllText(
                Path.Combine(dataDir, "shared-acrolinx-rules.txt"));
        }

        return prompt.Replace(AcrolinxToken, _acrolinxRules);
    }

    /// <summary>
    /// Resets cached content. Used in tests to ensure fresh state.
    /// </summary>
    internal static void ResetCache()
    {
        lock (_lock)
        {
            _acrolinxRules = null;
        }
    }
}
