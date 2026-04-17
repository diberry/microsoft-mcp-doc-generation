// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DocGeneration.TestInfrastructure;

/// <summary>
/// Shared utility for finding project roots by walking up from the base directory.
/// Consolidates duplicate FindProjectRoot() implementations across test projects.
/// </summary>
public static class ProjectRootFinder
{
    // .git covers both normal repos (directory) and worktrees (file).
    // docs-generation.sln kept as secondary marker for backward compatibility.
    private static readonly string[] SentinelMarkers = new[] { ".git", "docs-generation.sln" };

    /// <summary>
    /// Finds the repository root by looking for .git or docs-generation.sln.
    /// </summary>
    public static string FindSolutionRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (SentinelMarkers.Any(marker =>
                Directory.Exists(Path.Combine(dir, marker)) || File.Exists(Path.Combine(dir, marker))))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException(
            $"Could not find repo root by walking up from {AppContext.BaseDirectory}");
    }

    /// <summary>
    /// Finds the docs-generation/ subdirectory (where generator projects live).
    /// </summary>
    public static string FindDocsGenerationRoot()
    {
        return Path.Combine(FindSolutionRoot(), "docs-generation");
    }
}
