// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace CSharpGenerator.Generators;

/// <summary>
/// Thin wrapper that forwards to Shared.FrontmatterUtility.
/// Kept for backward compatibility with existing callers in CSharpGenerator.
/// </summary>
public static class FrontmatterUtility
{
    /// <summary>
    /// Generates frontmatter for tool annotation files.
    /// Delegates to Shared.FrontmatterUtility.GenerateAnnotationFrontmatter.
    /// </summary>
    public static string GenerateAnnotationFrontmatter(
        string toolCommand,
        string? version,
        string annotationFileName)
    {
        return Shared.FrontmatterUtility.GenerateAnnotationFrontmatter(
            toolCommand, version, annotationFileName);
    }

    /// <summary>
    /// Generates frontmatter for parameter documentation files.
    /// Delegates to Shared.FrontmatterUtility.GenerateParameterFrontmatter.
    /// </summary>
    public static string GenerateParameterFrontmatter(
        string toolCommand,
        string? version,
        string parameterFileName)
    {
        return Shared.FrontmatterUtility.GenerateParameterFrontmatter(
            toolCommand, version, parameterFileName);
    }
}
