// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Shared;

namespace Shared;

/// <summary>
/// Assembles CLI content blocks from AI-improved prose + deterministic parts.
/// All --switch names, commands, and table structure come from JSON (never from AI).
/// Only prose description fields come from AI improvement.
/// </summary>
public static class CliContentAssembler
{
    /// <summary>
    /// Assembles a CLI content block for a single tool.
    /// </summary>
    public static string AssembleCliContent(
        CliToolInfo tool,
        string? parameterCliContent = null,
        string? exampleCommandsContent = null)
    {
        var sb = new StringBuilder();

        // Example commands (deterministic, from include file or generated)
        if (!string.IsNullOrWhiteSpace(exampleCommandsContent))
        {
            sb.AppendLine(exampleCommandsContent.Trim());
            sb.AppendLine();
        }

        // Parameter table (deterministic structure, AI-improved descriptions)
        if (!string.IsNullOrWhiteSpace(parameterCliContent))
        {
            sb.AppendLine(parameterCliContent.Trim());
            sb.AppendLine();
        }
        else if (tool.Switches.Count > 0)
        {
            // Inline parameter table as fallback — filter infrastructure params and include Required column
            var filtered = GlobalSwitchFilter.FilterOutGlobal(tool.Switches);
            if (filtered.Count > 0)
            {
                sb.AppendLine("| Parameter | Type | Required | Description |");
                sb.AppendLine("|-----------|------|----------|-------------|");
                foreach (var sw in filtered)
                {
                    var desc = EscapePipe(sw.Description);
                    var required = sw.IsRequired == true ? "Yes" : "No";
                    sb.AppendLine($"| `{sw.Name}` | {sw.Type} | {required} | {desc} |");
                }
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Assembles CLI content blocks for all tools.
    /// Reads parameter-cli and example-commands include files from disk.
    /// </summary>
    public static async Task<Dictionary<string, string>> AssembleAllCliContentAsync(
        IReadOnlyDictionary<string, CliToolInfo> improvedTools,
        string parameterCliDir,
        string exampleCommandsDir,
        FileNameContext nameContext)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (command, tool) in improvedTools)
        {
            string? paramContent = null;
            string? exampleContent = null;

            var paramFile = Path.Combine(parameterCliDir,
                ToolFileNameBuilder.BuildParameterCliFileName(command, nameContext));
            if (File.Exists(paramFile))
                paramContent = FrontmatterUtility.StripFrontmatter(await File.ReadAllTextAsync(paramFile));

            var exampleFile = Path.Combine(exampleCommandsDir,
                ToolFileNameBuilder.BuildExampleCommandsFileName(command, nameContext));
            if (File.Exists(exampleFile))
                exampleContent = FrontmatterUtility.StripFrontmatter(await File.ReadAllTextAsync(exampleFile));

            result[CliJsonMapper.NormalizeCommand(command)] = AssembleCliContent(tool, paramContent, exampleContent);
        }

        return result;
    }

    /// <summary>
    /// Escapes pipe characters in text destined for markdown table cells.
    /// </summary>
    internal static string EscapePipe(string? text)
        => (text ?? "").Replace("|", "\\|");
}
