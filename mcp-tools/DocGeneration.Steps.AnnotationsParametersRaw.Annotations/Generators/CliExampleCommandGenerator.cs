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

        // Basic command
        sb.AppendLine("### Example CLI commands");
        sb.AppendLine();
        sb.AppendLine("Basic usage:");
        sb.AppendLine();
        sb.AppendLine("```azurecli");
        sb.AppendLine($"azmcp {command}");
        sb.AppendLine("```");

        // If tool has non-global switches, show a full example with placeholders
        var toolSpecificSwitches = tool.Switches
            .Where(s => !IsGlobalSwitch(s.Name))
            .ToList();

        if (toolSpecificSwitches.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("With parameters:");
            sb.AppendLine();
            sb.AppendLine("```azurecli");
            sb.Append($"azmcp {command}");
            foreach (var sw in toolSpecificSwitches)
            {
                var placeholder = sw.ValuePlaceholder ?? $"<{sw.Name.TrimStart('-')}>";
                sb.Append($" {sw.Name} {placeholder}");
            }
            sb.AppendLine();
            sb.AppendLine("```");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns true if the switch is a well-known global option.
    /// </summary>
    private static bool IsGlobalSwitch(string switchName) =>
        switchName is "--subscription" or "--tenant" or "--tenant-id"
            or "--auth-method" or "--retry-delay" or "--retry-max-delay"
            or "--retry-max-retries" or "--retry-mode" or "--retry-network-timeout";
}
