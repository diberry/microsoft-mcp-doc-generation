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

        // 1. Metadata section (frontmatter + H1 + intro — strip any H2s the AI may have generated)
        var metadataLines = familyContent.Metadata.Split('\n')
            .Where(line => !line.StartsWith("## "));
        sb.AppendLine(string.Join('\n', metadataLines));
        sb.AppendLine();

        // 2. Tool sections (H2 + content for each tool)
        foreach (var tool in familyContent.Tools)
        {
            sb.AppendLine(tool.Content);
            sb.AppendLine();
        }

        // 3. Related content section
        sb.AppendLine(familyContent.RelatedContent);

        // 4. Post-processing: expand all acronyms on first body mention (#142, #215)
        //    Replaces the old single-MCP expander with generalized AcronymExpander
        var markdown = sb.ToString().TrimEnd();
        markdown = AcronymExpander.ExpandAll(markdown);

        // 5. Post-processing: inject required frontmatter fields (#155)
        markdown = FrontmatterEnricher.Enrich(markdown);

        // 6. Post-processing: strip duplicate example blocks (#153)
        markdown = DuplicateExampleStripper.Strip(markdown);

        // 6a. Post-processing: strip engineering-authored example patterns (#278)
        markdown = EngineeringExampleStripper.Strip(markdown);

        // 7. Post-processing: ensure blank line between annotation link and values (#151)
        markdown = AnnotationSpaceFixer.Fix(markdown);

        // 7a. Post-processing: strip trailing pipe from annotation value lines (#281)
        markdown = AnnotationTrailingPipeFixer.Fix(markdown);

        // 8. Post-processing: convert future tense to present tense (#145, #215)
        markdown = PresentTenseFixer.Fix(markdown);

        // 9. Post-processing: apply contractions per Microsoft style guide (#145)
        markdown = ContractionFixer.Fix(markdown);

        // 9a. Post-processing: compound words, double-plurals, and wordy phrases (#393)
        markdown = StyleGuidePostProcessor.Fix(markdown);

        // 10. Post-processing: insert commas after introductory phrases (#146, #215)
        markdown = IntroductoryCommaFixer.Fix(markdown);

        // 10a. Post-processing: insert colon after "including" in intro paragraphs (#282)
        markdown = IncludingColonFixer.Fix(markdown);

        // 11. Post-processing: wrap bare example values in backticks (#152)
        markdown = ExampleValueBackticker.Fix(markdown);

        // 12. Post-processing: convert full learn.microsoft.com URLs to site-root-relative paths (#220, AD-017)
        markdown = LearnUrlRelativizer.Relativize(markdown);

        // 13. Post-processing: collapse inline JSON schemas in parameter tables (Acrolinx P1)
        markdown = JsonSchemaCollapser.Collapse(markdown);

        // 14. Post-processing: strip HTML scaffolding comments (preserve @mcpcli markers)
        markdown = ScaffoldingCommentStripper.Strip(markdown);

        // 15. Post-processing: escape bare <placeholder> values for MS Learn validation (#416)
        markdown = PlaceholderEscaper.Escape(markdown);

        return markdown;
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
