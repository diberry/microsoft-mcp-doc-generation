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
    private readonly Func<Task<Dictionary<string, BrandMapping>>> _loadBrandMappings;
    private readonly Func<Task<Dictionary<string, string>>> _loadCompoundWords;
    private readonly Func<string, Task<string>> _cleanFileName;
    private readonly Func<List<Tool>, List<CommonParameter>> _extractCommonParameters;

    public ParameterGenerator(
        Func<Task<Dictionary<string, BrandMapping>>> loadBrandMappings,
        Func<Task<Dictionary<string, string>>> loadCompoundWords,
        Func<string, Task<string>> cleanFileName,
        Func<List<Tool>, List<CommonParameter>> extractCommonParameters)
    {
        _loadBrandMappings = loadBrandMappings;
        _loadCompoundWords = loadCompoundWords;
        _cleanFileName = cleanFileName;
        _extractCommonParameters = extractCommonParameters;
    }

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
            
            // Load brand mappings
            var brandMappings = await _loadBrandMappings();
            
            // Get common parameters from CLI data only
            var commonParameters = data.SourceDiscoveredCommonParams;
            var commonParameterNames = new HashSet<string>(commonParameters.Select(p => p.Name ?? ""));
            
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

                // Create filename: {brand-filename}-{tool-family}-{operation}-parameters.md
                var fileName = !string.IsNullOrEmpty(cleanedRemainingParts)
                    ? $"{brandFileName}-{cleanedRemainingParts}-parameters.md"
                    : $"{brandFileName}-parameters.md";
                
                var outputFile = Path.Combine(outputDir, fileName);

                // Filter out common parameters unless they are required for this specific tool
                var allOptions = tool.Option ?? new List<Option>();
                
                // Filter to get only tool-specific parameters (non-common) or required common parameters
                var transformedOptions = allOptions
                    .Where(opt => !string.IsNullOrEmpty(opt.Name) && 
                                  (!commonParameterNames.Contains(opt.Name) || opt.Required == true))
                    .Select(opt => new
                    {
                        name = opt.Name,
                        NL_Name = TextCleanup.NormalizeParameter(opt.Name ?? ""),
                        type = opt.Type,
                        required = opt.Required,
                        RequiredText = opt.Required == true ? "Required" : "Optional",
                        description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(opt.Description ?? ""))
                    }).ToList();

                var parameterData = new Dictionary<string, object>
                {
                    ["tool"] = tool,
                    ["command"] = tool.Command ?? "",
                    ["area"] = tool.Area ?? "",
                    ["option"] = (object?)transformedOptions ?? new List<object>(),
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
            
            Console.WriteLine($"Generated {data.Tools.Count} parameter files in {outputDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating parameter files: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            throw;
        }
    }
}
