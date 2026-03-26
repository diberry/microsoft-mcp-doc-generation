// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CSharpGenerator.Models;

namespace CSharpGenerator.Generators;

/// <summary>
/// Shared predicate for filtering common parameters from tool-specific output.
/// Common (infrastructure) parameters are excluded unless they are required for
/// a specific tool — in that case they must appear in the parameter table.
/// </summary>
public static class ParameterFilterHelper
{
    /// <summary>
    /// Determines whether a parameter should be included in tool-specific output.
    /// A parameter is included when:
    ///   1. It has a non-empty name, AND
    ///   2. It is NOT a common parameter, OR it IS required for this tool.
    /// </summary>
    public static bool ShouldInclude(Option opt, HashSet<string> commonParameterNames)
    {
        return !string.IsNullOrEmpty(opt.Name)
            && (!commonParameterNames.Contains(opt.Name) || opt.Required);
    }
}
