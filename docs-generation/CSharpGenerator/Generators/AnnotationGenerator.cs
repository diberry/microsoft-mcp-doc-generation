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
/// Generates annotation files for MCP tools
/// </summary>
public class AnnotationGenerator
{

    /// <summary>
    /// Generates annotation files for all tools
    /// </summary>
    // NOTE: examplePromptGenerator parameter is deprecated and kept for backwards compatibility only
    // Use ExamplePromptGeneratorStandalone package instead
    public async Task GenerateAnnotationFilesAsync(
        TransformedData data, 
        string outputDir, 
        string templateFile, 
        object? examplePromptGenerator = null, 
        string? examplePromptsDir = null)
    {
        try
        {
            // Only show essential progress on console
            Console.WriteLine($"Generating annotation files for {data.Tools.Count} tools...");
            
            int examplePromptsGenerated = 0;
            int examplePromptsFailed = 0;
            
            // Log example prompts configuration to file
            LogFileHelper.WriteDebug($"examplePromptGenerator is {(examplePromptGenerator == null ? "NULL" : "initialized")}");
            LogFileHelper.WriteDebug($"examplePromptsDir = '{examplePromptsDir ?? "NULL"}'");
            if (examplePromptGenerator != null && !string.IsNullOrEmpty(examplePromptsDir))
            {
                LogFileHelper.WriteDebug("Example prompts WILL be generated for each tool");
            }
            else
            {
                LogFileHelper.WriteDebug("Example prompts WILL NOT be generated (missing generator or directory)");
            }
            
            // Track missing brand mappings/compound words
            var missingMappings = new Dictionary<string, List<string>>(); // area -> list of tool commands
            
            // Load shared context for filename generation and missing mapping tracking
            var nameContext = await FileNameContext.CreateAsync();

            foreach (var tool in data.Tools)
            {
                if (string.IsNullOrEmpty(tool.Command))
                    continue;

                var command = tool.Command;

                // Track missing brand mappings
                var area = command.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                if (!nameContext.BrandMappings.ContainsKey(area) || string.IsNullOrEmpty(nameContext.BrandMappings[area].FileName))
                {
                    var areaLower = area.ToLowerInvariant();
                    if (!nameContext.CompoundWords.ContainsKey(areaLower))
                    {
                        LogFileHelper.WriteDebug($"No brand mapping or compound word found for area '{area}', using '{areaLower}'");
                        if (!missingMappings.ContainsKey(area))
                        {
                            missingMappings[area] = new List<string>();
                        }
                        missingMappings[area].Add(command);
                    }
                    else
                    {
                        LogFileHelper.WriteDebug($"Applied compound word transformation for '{area}': '{areaLower}' -> '{nameContext.CompoundWords[areaLower]}'");
                    }
                }

                // Use shared deterministic filename builder
                var fileName = ToolFileNameBuilder.BuildAnnotationFileName(command, nameContext);
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

                var annotationData = new Dictionary<string, object>
                {
                    ["tool"] = tool,
                    ["metadata"] = formattedMetadata,
                    ["command"] = tool.Command ?? "",
                    ["area"] = tool.Area ?? "",
                    ["generateAnnotation"] = true,
                    ["generatedAt"] = data.GeneratedAt,
                    ["version"] = data.Version ?? "unknown",
                    ["annotationFileName"] = fileName
                };

                var templateResult = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, annotationData);
                var frontmatter = FrontmatterUtility.GenerateAnnotationFrontmatter(
                    tool.Command ?? "unknown",
                    data.Version,
                    fileName);
                var result = frontmatter + templateResult;
                await File.WriteAllTextAsync(outputFile, result);
                tool.HasAnnotation = true;
                
                // Generate example prompts if requested
                // DEPRECATED: Example prompts generation moved to ExamplePromptGeneratorStandalone package
                // Keeping reference for backwards compatibility but disabled
                /*
                if (examplePromptGenerator != null && !string.IsNullOrEmpty(examplePromptsDir))
                {
                    var (successCount, failureCount) = await examplePromptGenerator.GenerateExamplePromptFileAsync(
                        tool,
                        examplePromptsDir,
                        fileName,
                        data.Version,
                        HandlebarsTemplateEngine.ProcessTemplateAsync);
                    examplePromptsGenerated += successCount;
                    examplePromptsFailed += failureCount;
                }
                */
            }
            
            Console.WriteLine($"✓ Generated {data.Tools.Count} annotation files");
            
            // Log example prompts summary to file
            if (examplePromptGenerator != null)
            {
                LogFileHelper.WriteDebug("");
                LogFileHelper.WriteDebug("=== Example Prompts Summary ===");
                LogFileHelper.WriteDebug($"  Total tools processed: {data.Tools.Count}");
                LogFileHelper.WriteDebug($"  Successfully generated: {examplePromptsGenerated}");
                LogFileHelper.WriteDebug($"  Failed: {examplePromptsFailed}");
                LogFileHelper.WriteDebug($"  Output directory: {examplePromptsDir}");
            }
            
            // Generate missing mappings report if there are any
            if (missingMappings.Any())
            {
                await GenerateMissingMappingsReportAsync(missingMappings, outputDir);
            }
        }
        catch (Exception ex)
        {
            LogFileHelper.WriteDebug($"Error generating annotation files: {ex.Message}");
            LogFileHelper.WriteDebug(ex.StackTrace ?? "No stack trace");
            throw;
        }
    }

    /// <summary>
    /// Generates a summary page of all tool annotations
    /// </summary>
    public async Task GenerateToolAnnotationsSummaryAsync(
        TransformedData data, 
        string outputDir, 
        string templateFile, 
        string annotationsDir)
    {
        try
        {
            // Load shared data files for filename generation
            var nameContext = await FileNameContext.CreateAsync();

            // Collect all tools from all areas
            var allTools = new List<object>();
            
            foreach (var area in data.Areas)
            {
                foreach (var tool in area.Value.Tools)
                {
                    // Use shared deterministic filename builder
                    var annotationFileName = ToolFileNameBuilder.BuildAnnotationFileName(
                        tool.Command ?? "", nameContext);
                    var annotationFilePath = Path.Combine(annotationsDir, annotationFileName);
                    
                    string? annotationContent = null;
                    if (File.Exists(annotationFilePath))
                    {
                        annotationContent = await File.ReadAllTextAsync(annotationFilePath);
                    }
                    
                    allTools.Add(new
                    {
                        command = tool.Command ?? "Unknown",
                        description = tool.Description,
                        area = area.Key,
                        annotationContent = annotationContent,
                        annotationFileName = annotationFileName
                    });
                }
            }

            var pageData = new Dictionary<string, object>
            {
                ["version"] = data.Version,
                ["generatedAt"] = data.GeneratedAt,
                ["tools"] = allTools,
                ["toolCount"] = allTools.Count
            };

            var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, pageData);
            var outputFile = Path.Combine(outputDir, "tool-annotations.md");
            await File.WriteAllTextAsync(outputFile, result);
            
            LogFileHelper.WriteDebug($"Generated tool annotations summary at {outputFile}");
        }
        catch (Exception ex)
        {
            LogFileHelper.WriteDebug($"Error generating tool annotations summary: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Generates a report of missing brand mappings
    /// </summary>
    private async Task GenerateMissingMappingsReportAsync(Dictionary<string, List<string>> missingMappings, string outputDir)
    {
        try
        {
            var reportPath = Path.Combine(outputDir, "missing-brand-mappings.txt");
            var lines = new List<string>
            {
                "Missing Brand Mappings or Compound Words Report",
                "==============================================",
                "",
                $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
                "",
                "The following areas do not have brand mappings or compound word definitions:",
                ""
            };

            foreach (var kvp in missingMappings.OrderBy(m => m.Key))
            {
                lines.Add($"Area: {kvp.Key}");
                lines.Add($"  Commands affected:");
                foreach (var command in kvp.Value)
                {
                    lines.Add($"    - {command}");
                }
                lines.Add("");
            }

            lines.Add("Recommendations:");
            lines.Add("1. Add brand mapping to brand-to-server-mapping.json if this is a known Azure service");
            lines.Add("2. Add compound word transformation to compound-words.json if the area name needs special handling");
            lines.Add("3. Otherwise, the default lowercase area name will be used");

            await File.WriteAllLinesAsync(reportPath, lines);
            LogFileHelper.WriteDebug($"Generated missing mappings report: {reportPath}");
        }
        catch (Exception ex)
        {
            LogFileHelper.WriteDebug($"Failed to generate missing mappings report: {ex.Message}");
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
