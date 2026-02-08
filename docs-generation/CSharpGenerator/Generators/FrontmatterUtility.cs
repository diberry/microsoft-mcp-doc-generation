// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSharpGenerator.Generators;

/// <summary>
/// Utility class for generating frontmatter metadata for documentation files
/// </summary>
public static class FrontmatterUtility
{
    /// <summary>
    /// Generates frontmatter for input prompt files (stored in example-prompts-prompts/)
    /// </summary>
    /// <param name="toolCommand">The tool command (e.g., "azmcp storage account create")</param>
    /// <param name="version">The CLI version</param>
    /// <param name="inputPromptFileName">The input prompt filename</param>
    /// <param name="userPrompt">The user prompt used to generate the examples</param>
    /// <returns>Formatted frontmatter string with opening and closing "---" markers and userPrompt content</returns>
    public static string GenerateInputPromptFrontmatter(
        string toolCommand,
        string? version,
        string inputPromptFileName,
        string userPrompt)
    {
        var indentedUserPrompt = string.Join("\n", 
            userPrompt.Split('\n').Select(line => "  " + line));

        return $@"---
ms.topic: include
ms.date: {DateTime.UtcNow:yyyy-MM-dd}
mcp-cli.version: {version ?? "unknown"}
generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
# [!INCLUDE [{toolCommand}](../includes/tools/example-prompts-prompts/{inputPromptFileName})]
# azmcp {toolCommand}
userPrompt: |
{indentedUserPrompt}
---

";
    }

    /// <summary>
    /// Generates frontmatter for general documentation files
    /// </summary>
    /// <param name="topic">The MS topic type (e.g., "include", "article")</param>
    /// <param name="version">The CLI version</param>
    /// <param name="additionalMetadata">Optional additional metadata fields</param>
    /// <returns>Formatted frontmatter string with opening and closing "---" markers</returns>
    public static string GenerateGenericFrontmatter(
        string topic,
        string? version,
        Dictionary<string, string>? additionalMetadata = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"ms.topic: {topic}");
        sb.AppendLine($"ms.date: {DateTime.UtcNow:yyyy-MM-dd}");
        sb.AppendLine($"mcp-cli.version: {version ?? "unknown"}");
        sb.AppendLine($"generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        
        if (additionalMetadata != null)
        {
            foreach (var kvp in additionalMetadata)
            {
                sb.AppendLine($"{kvp.Key}: {kvp.Value}");
            }
        }
        
        sb.AppendLine("---");
        sb.AppendLine();
        
        return sb.ToString();
    }

    /// <summary>
    /// Generates frontmatter for tool annotation files
    /// </summary>
    /// <param name="toolCommand">The tool command</param>
    /// <param name="version">The CLI version</param>
    /// <param name="annotationFileName">The annotation filename</param>
    /// <returns>Formatted frontmatter string with opening and closing "---" markers</returns>
    public static string GenerateAnnotationFrontmatter(
        string toolCommand,
        string? version,
        string annotationFileName)
    {
        return $@"---
ms.topic: include
ms.date: {DateTime.UtcNow:yyyy-MM-dd}
mcp-cli.version: {version ?? "unknown"}
generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
# [!INCLUDE [{toolCommand}](../includes/tools/annotations/{annotationFileName})]
# azmcp {toolCommand}
---

";
    }

    /// <summary>
    /// Generates frontmatter for parameter documentation files
    /// </summary>
    /// <param name="toolCommand">The tool command</param>
    /// <param name="version">The CLI version</param>
    /// <param name="parameterFileName">The parameter filename</param>
    /// <returns>Formatted frontmatter string with opening and closing "---" markers</returns>
    public static string GenerateParameterFrontmatter(
        string toolCommand,
        string? version,
        string parameterFileName)
    {
        return $@"---
ms.topic: include
ms.date: {DateTime.UtcNow:yyyy-MM-dd}
mcp-cli.version: {version ?? "unknown"}
generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC
# [!INCLUDE [{toolCommand}](../includes/tools/parameters/{parameterFileName})]
# azmcp {toolCommand}
---

";
    }
}
