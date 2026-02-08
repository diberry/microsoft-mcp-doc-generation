using System.Text;

namespace ExamplePromptGeneratorStandalone.Utilities;

/// <summary>
/// Generates YAML frontmatter for markdown files.
/// </summary>
public static class FrontmatterUtility
{
    public static string GenerateInputPromptFrontmatter(
        string toolCommand,
        string? version,
        string inputPromptFileName,
        string userPrompt)
    {
        var indented = string.Join("\n", 
            userPrompt.Split('\n').Select(line => "  " + line));

        return $@"---
ms.topic: include
ms.date: {DateTime.UtcNow:yyyy-MM-dd}
mcp-cli.version: {version ?? "unknown"}
generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
# [!INCLUDE [{toolCommand}](../includes/tools/example-prompts-prompts/{inputPromptFileName})]
# azmcp {toolCommand}
userPrompt: |
{indented}
---

";
    }

    public static string GenerateExamplePromptsFrontmatter(
        string? version)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("ms.topic: include");
        sb.AppendLine($"ms.date: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"mcp-cli.version: {version ?? "unknown"}");
        sb.AppendLine("---");
        sb.AppendLine();
        return sb.ToString();
    }
}
