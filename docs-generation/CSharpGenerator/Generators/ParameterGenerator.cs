// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CSharpGenerator.Models;
using NaturalLanguageGenerator;
using Shared;
using static CSharpGenerator.Generators.FrontmatterUtility;

namespace CSharpGenerator.Generators;

/// <summary>
/// Generates parameter files for MCP tools
/// </summary>
public class ParameterGenerator
{
    /// <summary>
    /// Generates parameter files for all tools
    /// </summary>
    public async Task GenerateParameterFilesAsync(
        TransformedData data, 
        string outputDir, 
        string templateFile)
    {
        try
        {
            Console.WriteLine($"Generating parameter files for {data.Tools.Count} tools...");
            
            // Load shared data files for filename generation
            var nameContext = await FileNameContext.CreateAsync();
            
            // Get common parameters from CLI data only
            var commonParameters = data.SourceDiscoveredCommonParams;
            var commonParameterNames = new HashSet<string>(commonParameters.Select(p => p.Name ?? ""));
            
            foreach (var tool in data.Tools)
            {
                if (string.IsNullOrEmpty(tool.Command))
                    continue;

                // Use shared deterministic filename builder
                var fileName = ToolFileNameBuilder.BuildParameterFileName(
                    tool.Command, nameContext);
                var outputFile = Path.Combine(outputDir, fileName);

                // Filter out common parameters unless they are required for this specific tool
                var allOptions = tool.Option ?? new List<Option>();
                
                // Filter to get only tool-specific parameters (non-common) or required common parameters
                var conditionalParameters = new HashSet<string>(
                    tool.ConditionalRequiredParameters ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase);

                var filteredOptions = allOptions
                    .Where(opt => !string.IsNullOrEmpty(opt.Name) && 
                                  (!commonParameterNames.Contains(opt.Name) || opt.Required == true));

                var transformedOptions = filteredOptions
                    .Select(opt => new
                    {
                        name = opt.Name,
                        NL_Name = TextCleanup.NormalizeParameter(opt.Name ?? ""),
                        type = opt.Type,
                        required = opt.Required,
                        RequiredText = BuildRequiredText(opt.Required, opt.Name ?? "", conditionalParameters),
                        description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(opt.Description ?? ""))
                    })
                    .ToList();

                var parameterData = new Dictionary<string, object>
                {
                    ["tool"] = tool,
                    ["command"] = tool.Command ?? "",
                    ["area"] = tool.Area ?? "",
                    ["option"] = (object?)transformedOptions ?? new List<object>(),
                    ["hasConditionalRequired"] = transformedOptions?.Any(o => o.RequiredText.EndsWith("*", StringComparison.Ordinal)) ?? false,
                    ["generateParameter"] = true,
                    ["generatedAt"] = data.GeneratedAt,
                    ["version"] = data.Version ?? "unknown",
                    ["parameterFileName"] = fileName
                };

                var templateResult = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, parameterData);
                var frontmatter = FrontmatterUtility.GenerateParameterFrontmatter(
                    tool.Command ?? "unknown",
                    data.Version,
                    fileName);
                var result = frontmatter + templateResult;
                await File.WriteAllTextAsync(outputFile, result);
                tool.HasParameters = true;
            }
            
            Console.WriteLine($"✓ Generated {data.Tools.Count} parameter files");
        }
        catch (Exception ex)
        {
            LogFileHelper.WriteDebug($"Error generating parameter files: {ex.Message}");
            LogFileHelper.WriteDebug(ex.StackTrace ?? "No stack trace");
            throw;
        }
    }

    private static string BuildRequiredText(bool required, string parameterName, HashSet<string> conditionalParameters)
    {
        var baseText = required ? "Required" : "Optional";
        if (conditionalParameters.Contains(parameterName))
        {
            return baseText + "*";
        }

        return baseText;
    }
}
