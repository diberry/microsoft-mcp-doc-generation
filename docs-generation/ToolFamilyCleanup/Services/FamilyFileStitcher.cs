// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using ToolFamilyCleanup.Models;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Stitches together metadata, tool sections, and related content into a complete tool family markdown file.
/// No AI involved - pure string assembly.
/// </summary>
public class FamilyFileStitcher
{
    /// <summary>
    /// Assembles a complete tool family markdown file from its parts.
    /// </summary>
    /// <param name="familyContent">Family content with all parts</param>
    /// <returns>Complete markdown string</returns>
    public string Stitch(FamilyContent familyContent)
    {
        var sb = new StringBuilder();

        // 1. Metadata section (frontmatter + H1 + intro)
        sb.AppendLine(familyContent.Metadata);
        sb.AppendLine();

        // 2. Tool sections (H2 + content for each tool)
        foreach (var tool in familyContent.Tools)
        {
            sb.AppendLine(tool.Content);
            sb.AppendLine();
        }

        // 3. Related content section
        sb.AppendLine(familyContent.RelatedContent);

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Stitches and saves to file in one operation.
    /// </summary>
    /// <param name="familyContent">Family content to stitch</param>
    /// <param name="outputPath">Output file path</param>
    public async Task StitchAndSaveAsync(FamilyContent familyContent, string outputPath)
    {
        var markdown = Stitch(familyContent);
        await File.WriteAllTextAsync(outputPath, markdown);
    }
}
