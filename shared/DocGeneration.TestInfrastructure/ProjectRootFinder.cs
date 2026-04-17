// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DocGeneration.TestInfrastructure;

/// <summary>
/// Shared utility for finding project roots by walking up from the base directory.
/// Consolidates duplicate FindProjectRoot() implementations across test projects.
/// </summary>
public static class ProjectRootFinder
{
    private const string SolutionFileName = "docs-generation.sln";

    /// <summary>
    /// Finds the repository root (the directory containing docs-generation.sln).
    /// </summary>
    public static string FindSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, SolutionFileName)))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException(
            $"Could not find {SolutionFileName} by walking up from {AppContext.BaseDirectory}");
    }

    /// <summary>
    /// Finds the docs-generation/ subdirectory (where generator projects live).
    /// </summary>
    public static string FindDocsGenerationRoot()
    {
        return Path.Combine(FindSolutionRoot(), "docs-generation");
    }
}
