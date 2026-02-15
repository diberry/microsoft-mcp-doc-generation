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
/// Generates various documentation pages (area pages, index, commands, common tools, service options)
/// </summary>
public class PageGenerator
{
    private readonly Func<Task<Dictionary<string, BrandMapping>>> _loadBrandMappings;
    private readonly Func<string, Task<string>> _cleanFileName;
    private readonly Func<List<Tool>, List<CommonParameter>> _extractCommonParameters;

    public PageGenerator(
        Func<Task<Dictionary<string, BrandMapping>>> loadBrandMappings,
        Func<string, Task<string>> cleanFileName,
        Func<List<Tool>, List<CommonParameter>> extractCommonParameters)
    {
        _loadBrandMappings = loadBrandMappings;
        _cleanFileName = cleanFileName;
        _extractCommonParameters = extractCommonParameters;
    }

    /// <summary>
    /// Generates a documentation page for a specific service area
    /// </summary>
    public async Task GenerateAreaPageAsync(
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
            var outputFile = Path.Combine(outputDir, fileName);

            // Get common parameter names to filter them out
            var commonParameters = data.SourceDiscoveredCommonParams.Any() 
                ? data.SourceDiscoveredCommonParams 
                : _extractCommonParameters(data.Tools);
            var commonParameterNames = new HashSet<string>(commonParameters.Select(p => p.Name ?? ""));

            // Annotations directory path (at parent level)
            var parentDirCandidate = Path.GetDirectoryName(outputDir);
            var parentDir = string.IsNullOrWhiteSpace(parentDirCandidate) ? outputDir : parentDirCandidate;
            var annotationsDir = Path.Combine(parentDir, "annotations");
            
            // Load brand mappings for annotation filename lookup
            var brandMappings = await _loadBrandMappings();

            // Filter out common parameters from tools for area pages and add annotation content
            var toolsWithFilteredParamsTasks = areaData.Tools.Select(async tool => 
            {
                var filteredTool = new Tool
                {
                    Name = tool.Name,
                    Command = tool.Command,
                    Description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(tool.Description ?? "")),
                    SourceFile = tool.SourceFile,
                    Area = tool.Area,
                    Metadata = tool.Metadata
                };
                
                // Read annotation file content if it exists
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

                        // Clean the filename to match the annotation file generation
                        var cleanedRemainingParts = !string.IsNullOrEmpty(remainingParts) 
                            ? await _cleanFileName(remainingParts) 
                            : "";

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
                                // Silently ignore if annotation file can't be read
                                filteredTool.AnnotationContent = "";
                            }
                        }
                    }
                }
                
                if (tool.Option != null)
                {
                    filteredTool.Option = tool.Option
                        .Where(opt => !string.IsNullOrEmpty(opt.Name) && !commonParameterNames.Contains(opt.Name))
                        .Select(opt => new Option
                        {
                            Name = opt.Name,
                            NL_Name = TextCleanup.NormalizeParameter(opt.Name ?? ""),
                            Type = opt.Type,
                            Required = opt.Required,
                            RequiredText = opt.Required == true ? "Required" : "Optional",
                            Description = TextCleanup.EnsureEndsPeriod(TextCleanup.ReplaceStaticText(opt.Description ?? "")),
                        })
                        .OrderByDescending(opt => opt.Required) // Required parameters first
                        .ThenBy(opt => opt.NL_Name, StringComparer.OrdinalIgnoreCase) // Then alphabetically by natural language name
                        .ToList();
                }
                
                return filteredTool;
            });

            var toolsWithFilteredParams = (await Task.WhenAll(toolsWithFilteredParamsTasks)).ToList();

            var areaPageData = new Dictionary<string, object>
            {
                ["areaName"] = areaName,
                ["areaData"] = areaData,
                ["tools"] = toolsWithFilteredParams,
                ["version"] = data.Version,
                ["generatedAt"] = data.GeneratedAt,
                ["generateAreaPage"] = true
            };

            var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, areaPageData);

            await File.WriteAllTextAsync(outputFile, result);
            LogFileHelper.WriteInfo($"Generated area page: {fileName}");
        }
        catch (Exception ex)
        {
            LogFileHelper.WriteError($"Error generating area page for {areaName}: {ex.Message}");
            LogFileHelper.WriteError(ex.StackTrace ?? "No stack trace");
            throw;
        }
    }

    /// <summary>
    /// Generates the common tools documentation page
    /// </summary>
    public async Task GenerateCommonToolsPageAsync(
        TransformedData data, 
        string outputDir, 
        string templateFile)
    {
        // Use source-discovered parameters if available, otherwise fall back to CLI-discovered
        var commonParameters = data.SourceDiscoveredCommonParams.Any() 
            ? data.SourceDiscoveredCommonParams 
            : _extractCommonParameters(data.Tools);
        
        var commonPageData = new Dictionary<string, object>
        {
            ["version"] = data.Version,
            ["generatedAt"] = data.GeneratedAt,
            ["commonParameters"] = commonParameters
        };

        var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, commonPageData);

        // Generate in common-general directory under the provided output folder
        var commonGeneralDir = Path.Combine(outputDir, "common-general");
        Directory.CreateDirectory(commonGeneralDir);
        var outputFile = Path.Combine(commonGeneralDir, "common-tools.md");
        await File.WriteAllTextAsync(outputFile, result);
        LogFileHelper.WriteInfo("Generated common tools page: common-general/common-tools.md");
    }

    /// <summary>
    /// Generates the index documentation page
    /// </summary>
    public async Task GenerateIndexPageAsync(
        TransformedData data, 
        string outputDir, 
        string templateFile)
    {
        var indexPageData = new Dictionary<string, object>
        {
            ["version"] = data.Version,
            ["generatedAt"] = data.GeneratedAt,
            ["tools"] = data.Tools,
            ["areas"] = data.Areas,
            ["generateIndex"] = true
        };

        var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, indexPageData);

        // Generate in common-general directory under the provided output folder
        var commonGeneralDir = Path.Combine(outputDir, "common-general");
        Directory.CreateDirectory(commonGeneralDir);
        var outputFile = Path.Combine(commonGeneralDir, "index.md");
        await File.WriteAllTextAsync(outputFile, result);
        LogFileHelper.WriteInfo("Generated index page: common-general/index.md");
    }

    /// <summary>
    /// Generates the commands documentation page
    /// </summary>
    public async Task GenerateCommandsPageAsync(
        TransformedData data, 
        string outputDir, 
        string templateFile)
    {
        // Use source-discovered parameters if available, otherwise fall back to CLI-discovered
        var commonParameters = data.SourceDiscoveredCommonParams.Any() 
            ? data.SourceDiscoveredCommonParams 
            : _extractCommonParameters(data.Tools);
        
        var commandsPageData = new Dictionary<string, object>
        {
            ["version"] = data.Version,
            ["generatedAt"] = data.GeneratedAt,
            ["tools"] = data.Tools,
            ["areas"] = data.Areas,
            ["commonParameters"] = commonParameters
        };

        var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, commandsPageData);

        // Generate in common-general directory under the provided output folder
        var commonGeneralDir = Path.Combine(outputDir, "common-general");
        Directory.CreateDirectory(commonGeneralDir);
        var outputFile = Path.Combine(commonGeneralDir, "azmcp-commands.md");
        await File.WriteAllTextAsync(outputFile, result);
        LogFileHelper.WriteInfo("Generated commands page: common-general/azmcp-commands.md");
    }

    /// <summary>
    /// Generates the service options documentation page
    /// </summary>
    public async Task GenerateServiceOptionsPageAsync(
        TransformedData data, 
        string outputDir, 
        string templateFile)
    {
        try
        {
            // Get service options from source
            var serviceOptions = await ServiceOptionsDiscovery.DiscoverServiceStartOptionsFromSource();
            
            var serviceOptionsPageData = new Dictionary<string, object>
            {
                ["version"] = data.Version,
                ["generatedAt"] = data.GeneratedAt,
                ["serviceOptions"] = serviceOptions
            };

            var result = await HandlebarsTemplateEngine.ProcessTemplateAsync(templateFile, serviceOptionsPageData);

            // Generate in common-general directory under the provided output folder
            var commonGeneralDir = Path.Combine(outputDir, "common-general");
            Directory.CreateDirectory(commonGeneralDir);
            var outputFile = Path.Combine(commonGeneralDir, "service-start-option.md");
            await File.WriteAllTextAsync(outputFile, result);
            LogFileHelper.WriteInfo("Generated service options page: common-general/service-start-option.md");
        }
        catch (Exception ex)
        {
            LogFileHelper.WriteError($"Error generating service options page: {ex.Message}");
            LogFileHelper.WriteError(ex.StackTrace ?? "No stack trace");
            throw;
        }
    }
}
