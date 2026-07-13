// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DocGeneration.TestInfrastructure;

/// <summary>
/// Locates generated namespace output for tests that validate on-disk artifacts.
/// </summary>
public static class OutputArtifactLocator
{
    public const string OutputRootEnvironmentVariable = "DOCGEN_GENERATED_OUTPUT_ROOT";

    public static string GetOutputRoot()
    {
        var overrideRoot = Environment.GetEnvironmentVariable(OutputRootEnvironmentVariable);
        return string.IsNullOrWhiteSpace(overrideRoot)
            ? ProjectRootFinder.FindSolutionRoot()
            : Path.GetFullPath(overrideRoot);
    }

    public static IReadOnlyList<string> GetNamespaceDirectories()
    {
        var outputRoot = GetOutputRoot();
        if (!Directory.Exists(outputRoot))
        {
            return Array.Empty<string>();
        }

        return Directory.GetDirectories(outputRoot, "generated-*")
            .Where(static dir => !string.Equals(Path.GetFileName(dir), "generated", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static dir => dir, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
