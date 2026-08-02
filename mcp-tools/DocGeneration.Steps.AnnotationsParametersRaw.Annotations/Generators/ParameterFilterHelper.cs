// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using CSharpGenerator.Models;

namespace CSharpGenerator.Generators;

/// <summary>
/// Shared predicate for including parameters in tool-specific output.
/// Common parameters are no longer filtered; every named parameter appears in
/// the generated tables and counts.
/// </summary>
public static class ParameterFilterHelper
{
    /// <summary>
    /// Determines whether a parameter should be included in tool-specific output.
    /// A parameter is included when it has a non-empty name.
    /// </summary>
    public static bool ShouldInclude(Option opt, HashSet<string> commonParameterNames)
    {
        return !string.IsNullOrEmpty(opt.Name);
    }
}
