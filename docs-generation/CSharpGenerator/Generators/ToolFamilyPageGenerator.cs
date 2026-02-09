// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CSharpGenerator.Models;
using NaturalLanguageGenerator;
using Shared;

namespace CSharpGenerator.Generators;

/// <summary>
/// Generates enhanced tool family pages with example prompts and annotations
/// </summary>
public class ToolFamilyPageGenerator
{
    private readonly Func<Task<Dictionary<string, BrandMapping>>> _loadBrandMappings;
    private readonly Func<string, Task<string>> _cleanFileName;
    private readonly Func<List<Tool>, List<CommonParameter>> _extractCommonParameters;

    public ToolFamilyPageGenerator(
        Func<Task<Dictionary<string, BrandMapping>>> loadBrandMappings,
        Func<string, Task<string>> cleanFileName,
        Func<List<Tool>, List<CommonParameter>> extractCommonParameters)
    {
        _loadBrandMappings = loadBrandMappings;
        _cleanFileName = cleanFileName;
        _extractCommonParameters = extractCommonParameters;
    }

    /// <summary>
    /// Generates an enhanced tool family page with example prompts and annotations
    /// </summary>
    public async Task GenerateAsync(
        string areaName,
        AreaData areaData,
        TransformedData data,
        string outputDir,
        string templateFile)
    {
        try
        {
            var areaNameForFile = areaName.ToLowerInvariant().Replace(" ", "-");
            var fileName = $"{areaNameForFile}.md";
            
            // Create tool-family subdirectory
            var toolFamilyDir = Path.Combine(outputDir, "tool-family");
            Directory.CreateDirectory(toolFamilyDir);
            
            var outputFile = Path.Combine(toolFamilyDir, fileName);

            // Get common parameter names to filter them out
            var commonParameters = data.SourceDiscoveredCommonParams.Any()
                ? data.SourceDiscoveredCommonParams
                : _extractCommonParameters(data.Tools);
            var commonParameterNames = new HashSet<string>(commonParameters.Select(p => p.Name ?? ""));

            // Directories for resources
            var parentDirCandidate = Path.GetDirectoryName(outputDir);
            var parentDir = string.IsNullOrWhiteSpace(parentDirCandidate) ? outputDir : parentDirCandidate;
            var annotationsDir = Path.Combine(outputDir, "annotations");
            var examplePromptsDir = Path.Combine(outputDir, "example-prompts");
            var parametersDir = Path.Combine(outputDir, "parameters");

            // Load brand mappings for filename lookup
            var brandMappings = await _loadBrandMappings();

            // Process tools: filter parameters, load example prompts and annotations
            var toolsWithContentTasks = areaData.Tools.Select(async tool =>
            {
                var filteredTool = new Tool
                {
                    Name = DisplayNameBuilder.BuildDisplayName(tool.Command, tool.Name),
                    Command = tool.Command,
                    Description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(tool.Description ?? "")),
                    ConditionalRequiredNote = tool.ConditionalRequiredNote,
                    ConditionalRequiredParameters = tool.ConditionalRequiredParameters,
                    HasConditionalRequired = tool.HasConditionalRequired,
                    SourceFile = tool.SourceFile,
                    Area = tool.Area,
                    Metadata = tool.Metadata
                };

                if (!string.IsNullOrEmpty(tool.Command))
                {
                    // Parse command to get brand-based filename
                    var commandParts = tool.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (commandParts.Length > 0)
                    {
                        var area = commandParts[0];

                        // Get brand-based filename from mapping
                        string brandFileName;
                        if (brandMappings.TryGetValue(area, out var mapping) && !string.IsNullOrEmpty(mapping.FileName))
                        {
                            brandFileName = mapping.FileName;
                        }
                        else
                        {
                            brandFileName = area.ToLowerInvariant();
                        }

                        // Ensure prefix 'azure-'
                        if (!brandFileName.StartsWith("azure-", StringComparison.OrdinalIgnoreCase))
                        {
                            brandFileName = $"azure-{brandFileName}";
                        }

                        // Build remaining parts
                        var remainingParts = commandParts.Length > 1
                            ? string.Join("-", commandParts.Skip(1)).ToLowerInvariant()
                            : "";

                        // Clean the filename to match the generated files
                        var cleanedRemainingParts = !string.IsNullOrEmpty(remainingParts)
                            ? await _cleanFileName(remainingParts)
                            : "";

                        // Load annotation content
                        var annotationFileName = !string.IsNullOrEmpty(cleanedRemainingParts)
                            ? $"{brandFileName}-{cleanedRemainingParts}-annotations.md"
                            : $"{brandFileName}-annotations.md";

                        var annotationFilePath = Path.Combine(annotationsDir, annotationFileName);
                        if (File.Exists(annotationFilePath))
                        {
                            try
                            {
                                filteredTool.AnnotationContent = File.ReadAllText(annotationFilePath);
                                filteredTool.AnnotationFileName = annotationFileName;
                            }
                            catch
                            {
                                filteredTool.AnnotationContent = "";
                            }
                        }

                        // Load example prompts content
                        var examplePromptsFileName = !string.IsNullOrEmpty(cleanedRemainingParts)
                            ? $"{brandFileName}-{cleanedRemainingParts}-example-prompts.md"
                            : $"{brandFileName}-example-prompts.md";

                        var examplePromptsFilePath = Path.Combine(examplePromptsDir, examplePromptsFileName);
                        if (File.Exists(examplePromptsFilePath))
                        {
                            try
                            {
                                var content = File.ReadAllText(examplePromptsFilePath);
                                // Strip frontmatter if present
                                filteredTool.ExamplePromptsContent = StripFrontmatter(content);
                                filteredTool.ExamplePromptsFileName = examplePromptsFileName;
                            }
                            catch
                            {
                                filteredTool.ExamplePromptsContent = "";
                            }
                        }

                        // Load parameters content
                        var parametersFileName = !string.IsNullOrEmpty(cleanedRemainingParts)
                            ? $"{brandFileName}-{cleanedRemainingParts}-parameters.md"
                            : $"{brandFileName}-parameters.md";

                        var parametersFilePath = Path.Combine(parametersDir, parametersFileName);
                        if (File.Exists(parametersFilePath))
                        {
                            try
                            {
                                var content = File.ReadAllText(parametersFilePath);
                                // Strip frontmatter if present
                                filteredTool.ParametersContent = StripFrontmatter(content);
                                filteredTool.ParametersFileName = parametersFileName;
                            }
                            catch
                            {
                                filteredTool.ParametersContent = "";
                            }
                        }
                    }
                }

                // No longer need to manually filter parameters - we use the pre-generated parameter file
                return filteredTool;
            });

            var toolsWithContent = (await Task.WhenAll(toolsWithContentTasks)).ToList();

            // Build template data
            var pageData = new Dictionary<string, object>
            {
                ["areaName"] = areaName,
                ["areaDescription"] = areaData.Description ?? "",
                ["tools"] = toolsWithContent,
                ["toolCount"] = toolsWithContent.Count,
                ["version"] = data.Version,
                ["generatedAt"] = data.GeneratedAt,
                ["generateToolFamilyPage"] = true
            };

            // Process template
            var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, pageData);

            // Write output
            await File.WriteAllTextAsync(outputFile, result);
            Console.WriteLine($"Generated tool family page: {fileName}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error generating tool family page for {areaName}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            throw;
        }
    }

    /// <summary>
    /// Removes frontmatter (YAML between --- markers) from content
    /// </summary>
    private string StripFrontmatter(string content)
    {
        if (string.IsNullOrEmpty(content))
            return content;

        const string delimiter = "---";
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

        if (lines.Length < 2 || lines[0].Trim() != delimiter)
            return content; // No frontmatter

        // Find closing delimiter
        int endIndex = -1;
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == delimiter)
            {
                endIndex = i;
                break;
            }
        }

        if (endIndex == -1)
            return content; // Malformed, return as-is

        // Skip frontmatter and join remaining lines
        var remainingLines = lines.Skip(endIndex + 1);
        return string.Join("\n", remainingLines).TrimStart();
    }
}
