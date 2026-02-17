// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Text;

namespace CSharpGenerator.Generators;

/// <summary>
/// Utility class for generating frontmatter metadata for documentation files
/// </summary>
public static class FrontmatterUtility
{

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
