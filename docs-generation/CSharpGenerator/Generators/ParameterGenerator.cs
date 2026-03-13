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
using TemplateEngine;
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
                var manifestFileName = ToolFileNameBuilder.BuildParameterManifestFileName(
                    tool.Command, nameContext);
                var outputFile = Path.Combine(outputDir, fileName);
                var manifestOutputFile = Path.Combine(outputDir, manifestFileName);

                // Filter out common parameters unless they are required for this specific tool
                var allOptions = tool.Option ?? new List<Option>();
                
                // Filter to get only tool-specific parameters (non-common) or required common parameters
                var conditionalParameters = new HashSet<string>(
                    tool.ConditionalRequiredParameters ?? new List<string>(),
                    StringComparer.OrdinalIgnoreCase);

                var filteredOptions = allOptions
                    .Where(opt => !string.IsNullOrEmpty(opt.Name) && 
                                  (!commonParameterNames.Contains(opt.Name) || opt.Required == true))
                    .ToList();

                var parameterManifest = BuildParameterManifest(filteredOptions, conditionalParameters);

                var transformedOptions = filteredOptions
                    .Zip(parameterManifest, (opt, manifest) => new
                    {
                        name = manifest.Name,
                        NL_Name = manifest.DisplayName,
                        type = opt.Type,
                        required = manifest.Required,
                        RequiredText = manifest.RequiredText,
                        description = manifest.Description
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
                await File.WriteAllTextAsync(
                    manifestOutputFile,
                    JsonSerializer.Serialize(parameterManifest, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true
                    }));
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

    internal static List<ParameterManifestEntry> BuildParameterManifest(
        IEnumerable<Option> options,
        HashSet<string> conditionalParameters)
    {
        return options
            .Select(opt =>
            {
                var parameterName = opt.Name ?? string.Empty;
                return new ParameterManifestEntry
                {
                    Name = parameterName,
                    DisplayName = TextCleanup.NormalizeParameter(parameterName),
                    Required = opt.Required,
                    RequiredText = BuildRequiredText(opt.Required, parameterName, conditionalParameters),
                    IsConditionalRequired = conditionalParameters.Contains(parameterName),
                    Description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(opt.Description ?? string.Empty))
                };
            })
            .ToList();
    }

    /// <summary>
    /// Builds the "Required or optional" column text for a parameter.
    /// 
    /// Parameters can be both optional and conditional. The requirement level is
    /// a combination of the base level (Required/Optional) and a conditional modifier (*).
    /// Possible outputs:
    ///   - "Required"  — always required.
    ///   - "Optional"  — never required.
    ///   - "Required*" — required, and part of a conditional group.
    ///   - "Optional*" — optional by default, but conditionally required depending on
    ///                    how other parameters in the group are used.
    /// 
    /// The asterisk (*) pairs with a footnote in the rendered parameter table that
    /// explains the conditional relationship (e.g., "At least one of the parameters
    /// marked with * is required").
    /// </summary>
    internal static string BuildRequiredText(bool required, string parameterName, HashSet<string> conditionalParameters)
    {
        var baseText = required ? "Required" : "Optional";
        if (conditionalParameters.Contains(parameterName))
        {
            return baseText + "*";
        }

        return baseText;
    }

    internal sealed class ParameterManifestEntry
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public bool Required { get; set; }
        public string RequiredText { get; set; } = string.Empty;
        public bool IsConditionalRequired { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
