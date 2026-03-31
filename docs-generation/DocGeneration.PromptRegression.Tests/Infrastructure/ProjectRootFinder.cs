namespace DocGeneration.PromptRegression.Tests.Infrastructure;

/// <summary>
/// Delegates to the shared <see cref="TestInfrastructure.ProjectRootFinder"/> utility.
/// Kept as a thin wrapper so existing callers (BaselineManager, PromptContentTests) compile unchanged.
/// </summary>
public static class ProjectRootFinder
{
    /// <summary>
    /// Finds the docs-generation/ directory by walking up to docs-generation.sln.
    /// </summary>
    public static string FindDocsGenerationRoot() =>
        TestInfrastructure.ProjectRootFinder.FindDocsGenerationRoot();

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
