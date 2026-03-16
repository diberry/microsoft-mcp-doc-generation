// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using CSharpGenerator.Models;
using NaturalLanguageGenerator;

namespace CSharpGenerator.Generators;

/// <summary>
/// Centralized parameter sorting logic. Ensures all generated documentation
/// lists required parameters before optional ones, with alphabetical
/// secondary ordering by normalized (human-readable) name.
/// </summary>
public static class ParameterSorting
{
    /// <summary>
    /// Sorts parameters so that required parameters appear first, then optional.
    /// Within each group, parameters are sorted alphabetically by their
    /// normalized (human-readable) name, using case-insensitive comparison.
    /// </summary>
    /// <param name="parameters">The parameters to sort.</param>
    /// <returns>Sorted parameter sequence (required first, then alphabetical).</returns>
    public static IOrderedEnumerable<Option> SortByRequiredThenName(
        IEnumerable<Option> parameters)
    {
        return parameters
            .OrderByDescending(p => p.Required)
            .ThenBy(p => TextCleanup.NormalizeParameter(p.Name ?? ""),
                    StringComparer.OrdinalIgnoreCase);
    }
}
