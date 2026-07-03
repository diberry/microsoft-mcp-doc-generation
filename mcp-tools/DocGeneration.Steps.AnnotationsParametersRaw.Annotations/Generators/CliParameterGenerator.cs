// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using Shared;
using TemplateEngine;

namespace CSharpGenerator.Generators;

/// <summary>
/// Generates CLI parameter files for MCP tools.
/// </summary>
public class CliParameterGenerator
{
    /// <summary>
    /// Generates CLI parameter files for all tools that have CLI data.
    /// </summary>
    public static async Task GenerateParameterCliFilesAsync(
        IReadOnlyDictionary<string, CliToolInfo> cliTools,
        string templateFile,
        string outputDir,
        FileNameContext nameContext,
        string cliVersion,
        DateTime generatedAt)
    {
        Directory.CreateDirectory(outputDir);

        foreach (var (command, tool) in cliTools)
        {
            var fileName = ToolFileNameBuilder.BuildParameterCliFileName(command, nameContext);
            var outputFile = Path.Combine(outputDir, fileName);

            var filtered = GlobalSwitchFilter.FilterOutGlobal(tool.Switches);
            var switches = filtered
                .Select(sw => new
                {
                    sw.Name,
                    DisplayName = CliParameterDisplayNameFormatter.StripCliPrefix(sw.Name),
                    sw.Description,
                    sw.Type,
                    sw.IsRequired,
                    sw.Default,
                    sw.ShortAlias,
                    sw.ValuePlaceholder,
                    sw.AllowedValues
                })
                .ToList();
            var templateData = new Dictionary<string, object>
            {
                ["hasParameters"] = switches.Count > 0,
                ["switches"] = switches,
                ["command"] = command,
                ["generatedAt"] = generatedAt,
                ["version"] = cliVersion
            };

            var templateResult = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, templateData);

            var frontmatter = $"""
                ---
                ms.topic: include
                ms.date: {generatedAt:MM/dd/yyyy}
                mcp-cli.version: {cliVersion}
                ---

                """;

            await File.WriteAllTextAsync(outputFile, frontmatter + templateResult, Encoding.UTF8);
        }
    }
}
