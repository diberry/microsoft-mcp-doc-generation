namespace ExamplePromptGeneratorStandalone.Utilities;

/// <summary>
/// Thin wrapper that forwards to Shared.FrontmatterUtility.
/// Kept for backward compatibility with existing callers in ExamplePromptGeneratorStandalone.
/// </summary>
public static class FrontmatterUtility
{
    public static string GenerateInputPromptFrontmatter(
        string toolCommand,
        string? version,
        string inputPromptFileName,
        string userPrompt)
    {
        return Shared.FrontmatterUtility.GenerateInputPromptFrontmatter(
            toolCommand, version, inputPromptFileName, userPrompt);
    }

    public static string GenerateExamplePromptsFrontmatter(string? version)
    {
        return Shared.FrontmatterUtility.GenerateExamplePromptsFrontmatter(version);
    }
}
