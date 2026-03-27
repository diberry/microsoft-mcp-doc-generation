namespace DocGeneration.PromptRegression.Tests.Infrastructure;

/// <summary>
/// Shared utility for finding project roots by walking up from the base directory.
/// </summary>
public static class ProjectRootFinder
{
    /// <summary>
    /// Finds the docs-generation/ directory by walking up to docs-generation.sln.
    /// </summary>
    public static string FindDocsGenerationRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "docs-generation.sln")))
                return Path.Combine(dir, "docs-generation");
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException("Could not find docs-generation.sln");
    }

    /// <summary>
    /// Finds the PromptRegression.Tests project root.
    /// </summary>
    public static string FindTestProjectRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "DocGeneration.PromptRegression.Tests.csproj")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException(
            "Could not find DocGeneration.PromptRegression.Tests project root.");
    }
}
