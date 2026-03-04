// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Shared;

/// <summary>
/// Centralized utility for generating YAML frontmatter metadata for documentation files.
/// All generators should use this instead of building frontmatter independently.
/// </summary>
public static class FrontmatterUtility
{
    /// <summary>
    /// Generates a YAML frontmatter block with standard metadata fields.
    /// </summary>
    /// <param name="msTopic">The ms.topic value (e.g., "include", "reference")</param>
    /// <param name="version">The MCP CLI version (null defaults to "unknown")</param>
    /// <param name="generatedDate">Pre-formatted date string for the generated field (null omits the field)</param>
    /// <param name="msDate">Pre-formatted date string for ms.date (null uses generatedDate)</param>
    /// <param name="yamlComments">Optional YAML comments to include before the closing --- (e.g., INCLUDE references)</param>
    /// <param name="extraFields">Optional additional YAML fields as key-value pairs</param>
    /// <returns>Formatted frontmatter string including opening/closing "---" markers and trailing newline</returns>
    public static string Generate(
        string msTopic,
        string? version,
        string? generatedDate = null,
        string? msDate = null,
        IEnumerable<string>? yamlComments = null,
        IEnumerable<KeyValuePair<string, string>>? extraFields = null)
    {
        var resolvedVersion = version ?? "unknown";
        var resolvedMsDate = msDate ?? generatedDate ?? DateTime.UtcNow.ToString("yyyy-MM-dd");

        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"ms.topic: {msTopic}");
        sb.AppendLine($"ms.date: {resolvedMsDate}");
        sb.AppendLine($"mcp-cli.version: {resolvedVersion}");

        if (generatedDate != null)
        {
            sb.AppendLine($"generated: {generatedDate}");
        }

        if (extraFields != null)
        {
            foreach (var field in extraFields)
            {
                sb.AppendLine($"{field.Key}: {field.Value}");
            }
        }

        if (yamlComments != null)
        {
            foreach (var comment in yamlComments)
            {
                sb.AppendLine(comment);
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Generates frontmatter for tool annotation include files.
    /// Output-compatible with CSharpGenerator.Generators.FrontmatterUtility.GenerateAnnotationFrontmatter.
    /// </summary>
    public static string GenerateAnnotationFrontmatter(
        string toolCommand,
        string? version,
        string annotationFileName)
    {
        var now = DateTime.UtcNow;
        return Generate(
            msTopic: "include",
            version: version,
            generatedDate: now.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
            msDate: now.ToString("yyyy-MM-dd"),
            yamlComments: new[]
            {
                $"# [!INCLUDE [{toolCommand}](../includes/tools/annotations/{annotationFileName})]",
                $"# azmcp {toolCommand}"
            });
    }

    /// <summary>
    /// Generates frontmatter for parameter documentation include files.
    /// Output-compatible with CSharpGenerator.Generators.FrontmatterUtility.GenerateParameterFrontmatter.
    /// </summary>
    public static string GenerateParameterFrontmatter(
        string toolCommand,
        string? version,
        string parameterFileName)
    {
        var now = DateTime.UtcNow;
        return Generate(
            msTopic: "include",
            version: version,
            generatedDate: now.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
            msDate: now.ToString("yyyy-MM-dd"),
            yamlComments: new[]
            {
                $"# [!INCLUDE [{toolCommand}](../includes/tools/parameters/{parameterFileName})]",
                $"# azmcp {toolCommand}"
            });
    }

    /// <summary>
    /// Generates frontmatter for example prompts include files (no generated field, no comments).
    /// Output-compatible with ExamplePromptGeneratorStandalone.Utilities.FrontmatterUtility.GenerateExamplePromptsFrontmatter.
    /// </summary>
    public static string GenerateExamplePromptsFrontmatter(string? version)
    {
        var now = DateTime.UtcNow;
        return Generate(
            msTopic: "include",
            version: version,
            msDate: now.ToString("yyyy-MM-dd HH:mm:ss") + " UTC");
    }

    /// <summary>
    /// Generates frontmatter for input prompt files with embedded user prompt content.
    /// Output-compatible with ExamplePromptGeneratorStandalone.Utilities.FrontmatterUtility.GenerateInputPromptFrontmatter.
    /// </summary>
    public static string GenerateInputPromptFrontmatter(
        string toolCommand,
        string? version,
        string inputPromptFileName,
        string userPrompt)
    {
        var now = DateTime.UtcNow;
        var indented = string.Join("\n",
            userPrompt.Split('\n').Select(line => "  " + line));

        return Generate(
            msTopic: "include",
            version: version,
            generatedDate: now.ToString("yyyy-MM-dd HH:mm:ss") + " UTC",
            msDate: now.ToString("yyyy-MM-dd"),
            yamlComments: new[]
            {
                $"# [!INCLUDE [{toolCommand}](../includes/tools/example-prompts-prompts/{inputPromptFileName})]",
                $"# azmcp {toolCommand}"
            },
            extraFields: new[]
            {
                new KeyValuePair<string, string>("userPrompt", $"|\n{indented}")
            });
    }

    /// <summary>
    /// Generates frontmatter for raw tool reference files.
    /// Output-compatible with ToolGeneration_Raw inline frontmatter.
    /// </summary>
    public static string GenerateRawToolFrontmatter(string? mcpCliVersion, string? generatedDate)
    {
        return Generate(
            msTopic: "reference",
            version: mcpCliVersion,
            generatedDate: generatedDate,
            msDate: generatedDate);
    }
}
