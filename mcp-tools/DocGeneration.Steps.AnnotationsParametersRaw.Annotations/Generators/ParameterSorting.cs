// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using CSharpGenerator.Models;

namespace CSharpGenerator.Generators;

/// <summary>
/// Centralized parameter sorting logic. Ensures all generated documentation
/// lists required parameters before optional ones while preserving the
/// source metadata order within each group.
/// </summary>
public static class ParameterSorting
{
    /// <summary>
    /// Sorts parameters so that required parameters appear first, then optional,
    /// preserving source metadata order within each group.
    /// </summary>
    /// <param name="parameters">The parameters to sort.</param>
    /// <returns>Sorted parameter sequence (required first, stable within each group).</returns>
    public static IOrderedEnumerable<Option> SortByRequiredThenName(
        IEnumerable<Option> parameters)
    {
        return parameters
            .OrderByDescending(p => p.Required);
    }

    /// <summary>
    /// Sorts CLI switches so that required switches appear first, then optional,
    /// preserving source metadata order within each group.
    /// </summary>
    /// <param name="switches">The CLI switches to sort.</param>
    /// <returns>Sorted switch sequence (required first, stable within each group).</returns>
    public static IOrderedEnumerable<Shared.CliSwitch> SortByRequiredThenName(
        IEnumerable<Shared.CliSwitch> switches)
    {
        return switches
            .OrderByDescending(s => s.IsRequired == true);
    }
}
