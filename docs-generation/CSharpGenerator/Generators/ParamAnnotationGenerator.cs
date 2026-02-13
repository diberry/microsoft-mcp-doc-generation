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

/*
DEPRECATED: Combined parameter and annotation file generation has been disabled.
Keeping code in place for reference but not used. 
Use separate annotations and parameters files instead (or complete tool files with --complete-tools flag).
See: UNUSED-FUNCTIONALITY.md for details.

/// <summary>
/// Generates combined parameter and annotation files for MCP tools
/// </summary>
public class ParamAnnotationGenerator
{
    private readonly Func<Task<Dictionary<string, BrandMapping>>> _loadBrandMappings;
    private readonly Func<Task<Dictionary<string, string>>> _loadCompoundWords;
    private readonly Func<string, Task<string>> _cleanFileName;

    public ParamAnnotationGenerator(
        Func<Task<Dictionary<string, BrandMapping>>> loadBrandMappings,
        Func<Task<Dictionary<string, string>>> loadCompoundWords,
        Func<string, Task<string>> cleanFileName)
    {
        _loadBrandMappings = loadBrandMappings;
        _loadCompoundWords = loadCompoundWords;
        _cleanFileName = cleanFileName;
    }

    /// <summary>
    /// Generates combined parameter and annotation files for all tools
    /// </summary>
    public async Task GenerateParamAnnotationFilesAsync(
        TransformedData data, 
        string outputDir, 
        string templateFile)
    {
        try
        {
            Console.WriteLine($"Generating parameter and annotation files for {data.Tools.Count} tools...");
            
            // Load brand mappings
            var brandMappings = await _loadBrandMappings();
            
            foreach (var tool in data.Tools)
            {
                if (string.IsNullOrEmpty(tool.Command))
                    continue;

                // Parse command to extract area (first part)
                var commandParts = tool.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (commandParts.Length == 0)
                    continue;

                var area = commandParts[0];
                
                // Load compound words for area name transformation
                var compoundWords = await _loadCompoundWords();
                
                // Get brand-based filename from mapping, or fall back to area name
                string brandFileName;
                if (brandMappings.TryGetValue(area, out var mapping) && !string.IsNullOrEmpty(mapping.FileName))
                {
                    brandFileName = mapping.FileName;
                }
                else
                {
                    // Fallback: use area name, but check compound words first
                    var areaLower = area.ToLowerInvariant();
                    if (compoundWords.TryGetValue(areaLower, out var compoundReplacement))
                    {
                        brandFileName = compoundReplacement;
                    }
                    else
                    {
                        brandFileName = areaLower;
                    }
                }

                // Build remaining parts of command (tool family and operation)
                var remainingParts = commandParts.Length > 1 
                    ? string.Join("-", commandParts.Skip(1)).ToLowerInvariant()
                    : "";

                // Clean the filename to remove stop words and separate smashed words
                var cleanedRemainingParts = !string.IsNullOrEmpty(remainingParts) 
                    ? await _cleanFileName(remainingParts) 
                    : "";

                // Create filename: {brand-filename}-{tool-family}-{operation}-param-annotation.md
                var fileName = !string.IsNullOrEmpty(cleanedRemainingParts)
                    ? $"{brandFileName}-{cleanedRemainingParts}-param-annotation.md"
                    : $"{brandFileName}-param-annotation.md";
                
                var outputFile = Path.Combine(outputDir, fileName);

                // Format metadata with display names for each property
                var formattedMetadata = new Dictionary<string, object>();
                var metadata = tool.Metadata ?? new ToolMetadata();
                
                if (metadata.Destructive != null)
                {
                    formattedMetadata["destructive"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("destructive"),
                        value = metadata.Destructive.Value,
                        description = metadata.Destructive.Description
                    };
                }
                
                if (metadata.Idempotent != null)
                {
                    formattedMetadata["idempotent"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("idempotent"),
                        value = metadata.Idempotent.Value,
                        description = metadata.Idempotent.Description
                    };
                }
                
                if (metadata.OpenWorld != null)
                {
                    formattedMetadata["openWorld"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("openWorld"),
                        value = metadata.OpenWorld.Value,
                        description = metadata.OpenWorld.Description
                    };
                }
                
                if (metadata.ReadOnly != null)
                {
                    formattedMetadata["readOnly"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("readOnly"),
                        value = metadata.ReadOnly.Value,
                        description = metadata.ReadOnly.Description
                    };
                }
                
                if (metadata.Secret != null)
                {
                    formattedMetadata["secret"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("secret"),
                        value = metadata.Secret.Value,
                        description = metadata.Secret.Description
                    };
                }
                
                if (metadata.LocalRequired != null)
                {
                    formattedMetadata["localRequired"] = new 
                    {
                        name = ConvertCamelCaseToTitleCase("localRequired"),
                        value = metadata.LocalRequired.Value,
                        description = metadata.LocalRequired.Description
                    };
                }

                // Transform options to include RequiredText
                var transformedOptions = tool.Option?.Select(opt => new
                {
                    name = opt.Name,
                    // IMPORTANT: Preserves ALL words including type qualifiers like "name"
                    NL_Name = TextCleanup.NormalizeParameter(opt.Name ?? ""),
                    type = opt.Type,
                    required = opt.Required,
                    RequiredText = opt.Required == true ? "Required" : "Optional",
                    description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(opt.Description ?? ""))
                }).ToList();

                var paramAnnotationData = new Dictionary<string, object>
                {
                    ["tool"] = tool,
                    ["metadata"] = formattedMetadata,
                    ["command"] = tool.Command ?? "",
                    ["area"] = tool.Area ?? "",
                    ["option"] = (object?)transformedOptions ?? new List<object>(),
                    ["generateParameterAndAnnotation"] = true,
                    ["generatedAt"] = data.GeneratedAt,
                    ["version"] = data.Version ?? "unknown",
                    ["paramAnnotationFileName"] = fileName
                };

                var templateResult = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, paramAnnotationData);
                var frontmatter = FrontmatterUtility.GenerateGenericFrontmatter(
                    "include",
                    data.Version,
                    new Dictionary<string, string>
                    {
                        ["command"] = tool.Command ?? "unknown",
                        ["fileName"] = fileName
                    });
                var result = frontmatter + templateResult;
                await File.WriteAllTextAsync(outputFile, result);
            }
            
            Console.WriteLine($"Generated {data.Tools.Count} parameter and annotation files in {outputDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating parameter and annotation files: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            throw;
        }
    }

    /// <summary>
    /// Converts camelCase to Title Case
    /// </summary>
    private static string ConvertCamelCaseToTitleCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var result = new System.Text.StringBuilder();
        result.Append(char.ToUpper(input[0]));
        
        for (int i = 1; i < input.Length; i++)
        {
            if (char.IsUpper(input[i]))
            {
                result.Append(' ');
            }
            result.Append(input[i]);
        }
        
        return result.ToString();
    }
}
*/
