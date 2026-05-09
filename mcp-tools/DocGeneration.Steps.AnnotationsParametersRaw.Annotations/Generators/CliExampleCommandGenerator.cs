// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Shared;

namespace CSharpGenerator.Generators;

/// <summary>
/// Generates example CLI command files showing usage with switches.
/// Fully deterministic — no AI, no templates needed.
/// </summary>
public static class CliExampleCommandGenerator
{
    /// <summary>
    /// Generates example CLI command files for all tools that have CLI data.
    /// </summary>
    public static async Task GenerateExampleCommandFilesAsync(
        IReadOnlyDictionary<string, CliToolInfo> cliTools,
        string outputDir,
        FileNameContext nameContext,
        string cliVersion,
        DateTime generatedAt)
    {
        Directory.CreateDirectory(outputDir);

        foreach (var (command, tool) in cliTools)
        {
            var fileName = ToolFileNameBuilder.BuildExampleCommandsFileName(command, nameContext);
            var outputFile = Path.Combine(outputDir, fileName);

            var content = BuildExampleCommandContent(command, tool);

            var frontmatter = $"""
                ---
                ms.topic: include
                ms.date: {generatedAt:MM/dd/yyyy}
                mcp-cli.version: {cliVersion}
                ---

                """;

            await File.WriteAllTextAsync(outputFile, frontmatter + content, Encoding.UTF8);
        }
    }

    /// <summary>
    /// Builds the example command markdown for a single tool.
    /// </summary>
    internal static string BuildExampleCommandContent(string command, CliToolInfo tool)
    {
        var sb = new StringBuilder();

        sb.AppendLine("**Example CLI command**");
        sb.AppendLine();
        sb.AppendLine("```azurecli");

        // Get non-global switches, required first then optional
        var toolSwitches = tool.Switches
            .Where(s => !IsGlobalSwitch(s.Name))
            .OrderByDescending(s => s.IsRequired == true)
            .ToList();

        if (toolSwitches.Count > 0)
        {
            sb.AppendLine($"azmcp {command} \\");
            for (int i = 0; i < toolSwitches.Count; i++)
            {
                var sw = toolSwitches[i];
                var placeholder = sw.ValuePlaceholder ?? $"<{sw.Name.TrimStart('-')}>";
                var isLast = (i == toolSwitches.Count - 1);
                var continuation = isLast ? "" : " \\";

                if (sw.IsRequired == true)
                {
                    sb.AppendLine($"  {sw.Name} {placeholder}{continuation}");
                }
                else
                {
                    sb.AppendLine($"  [{sw.Name} {placeholder}]{continuation}");
                }
            }
        }
        else
        {
            sb.AppendLine($"azmcp {command}");
        }

        sb.AppendLine("```");

        return sb.ToString();
    }

    /// <summary>
    /// Returns true if the switch is a well-known global option.
    /// </summary>
    private static bool IsGlobalSwitch(string switchName) =>
        GlobalSwitchFilter.IsGlobalSwitch(switchName);
}
