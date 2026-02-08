// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using ToolFamilyCleanup.Models;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Reads and parses complete tool markdown files from ./generated/tools directory.
/// Groups tools by family name extracted from filename prefix.
/// </summary>
public class ToolReader
{
    private static readonly Regex FrontmatterRegex = new(@"^---\s*\n.*?\n---\s*\n", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex ToolNameRegex = new(@"^#\s+(.+)$", RegexOptions.Multiline | RegexOptions.Compiled);
    private static readonly Regex CommandRegex = new(@"<!--\s*@mcpcli\s+([^>]+?)\s*-->", RegexOptions.Compiled);
    
    private readonly string _toolsDirectory;

    public ToolReader(string toolsDirectory)
    {
        // Resolve path relative to current working directory (where PowerShell script runs)
        // NOT relative to executable location
        _toolsDirectory = Path.IsPathRooted(toolsDirectory) 
            ? toolsDirectory 
            : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), toolsDirectory));
    }

    /// <summary>
    /// Reads all *.md files from tools directory and groups by family name.
    /// </summary>
    /// <returns>Dictionary mapping family name to list of tools</returns>
    public async Task<Dictionary<string, List<ToolContent>>> ReadAndGroupToolsAsync()
    {
        Console.WriteLine($"Reading tools from: {_toolsDirectory}");
        
        if (!Directory.Exists(_toolsDirectory))
        {
            throw new DirectoryNotFoundException($"Tools directory not found: {_toolsDirectory}");
        }

        var toolFiles = Directory.GetFiles(_toolsDirectory, "*.md", SearchOption.TopDirectoryOnly)
            .OrderBy(f => f)
            .ToList();

        if (toolFiles.Count == 0)
        {
            Console.WriteLine("⚠ No tool files found in directory.");
            return new Dictionary<string, List<ToolContent>>();
        }

        Console.WriteLine($"Found {toolFiles.Count} tool files to process");
        Console.WriteLine();

        var toolsByFamily = new Dictionary<string, List<ToolContent>>();
        int successCount = 0;
        int failCount = 0;

        foreach (var filePath in toolFiles)
        {
            try
            {
                var toolContent = await ParseToolFileAsync(filePath);
                
                if (!toolsByFamily.ContainsKey(toolContent.FamilyName))
                {
                    toolsByFamily[toolContent.FamilyName] = new List<ToolContent>();
                }
                
                toolsByFamily[toolContent.FamilyName].Add(toolContent);
                successCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠ Failed to parse {Path.GetFileName(filePath)}: {ex.Message}");
                failCount++;
            }
        }

        // Sort tools within each family alphabetically
        foreach (var family in toolsByFamily.Keys)
        {
            toolsByFamily[family] = toolsByFamily[family]
                .OrderBy(t => t.ToolName)
                .ToList();
        }

        Console.WriteLine($"✓ Parsed {successCount} tools into {toolsByFamily.Count} families");
        if (failCount > 0)
        {
            Console.WriteLine($"⚠ Failed to parse {failCount} tools");
        }
        Console.WriteLine();

        // Show summary
        foreach (var family in toolsByFamily.Keys.OrderBy(k => k))
        {
            Console.WriteLine($"  {family}: {toolsByFamily[family].Count} tools");
        }
        Console.WriteLine();

        return toolsByFamily;
    }

    /// <summary>
    /// Parses a single tool file and extracts metadata and content.
    /// </summary>
    private async Task<ToolContent> ParseToolFileAsync(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var content = await File.ReadAllTextAsync(filePath);

        // Extract family name from @mcpcli namespace in content
        // Fallback to filename when not present
        var familyName = ExtractFamilyNameFromContent(content)
            ?? ExtractFamilyNameFromFileName(fileName);

        // Extract tool name from H1 heading
        var toolName = ExtractToolName(content) ?? "Unknown Tool";
        var command = ExtractCommand(content);

        // Strip frontmatter
        var contentWithoutFrontmatter = StripFrontmatter(content);

        return new ToolContent
        {
            ToolName = toolName,
            FileName = fileName,
            FamilyName = familyName,
            Content = contentWithoutFrontmatter.Trim(),
            Command = command,
            Description = ExtractDescription(content),
            SourceFilePath = filePath
        };
    }

    /// <summary>
    /// Extracts family name from filename.
    /// Pattern: {family}-*.md
    /// Examples:
    ///   advisor-recommendation-list.md → advisor
    ///   storage-account-create.md → storage
    ///   ai-foundry-agents-connect.md → ai-foundry (special case)
    /// </summary>
    private string ExtractFamilyNameFromFileName(string fileName)
    {
        // Remove .md extension
        var nameWithoutExt = fileName.Replace(".md", "");
        
        // Split by hyphens
        var parts = nameWithoutExt.Split('-');
        
        if (parts.Length < 1)
        {
            return "unknown";
        }

        // Special case for multi-part service names like "ai-foundry"
        if (parts[0] == "ai" && parts.Length > 1)
        {
            // ai-foundry-... → ai-foundry
            return $"{parts[0]}-{parts[1]}";
        }
        
        // Default: first part is the family name
        return parts[0];
    }

    private string? ExtractFamilyNameFromContent(string content)
    {
        var match = Regex.Match(content, "@mcpcli\\s+([^\\r\\n]+)");
        if (!match.Success)
        {
            return null;
        }

        var commandText = match.Groups[1].Value.Trim();
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return null;
        }

        var namespaceToken = commandText.Split(' ')[0].Trim();
        return string.IsNullOrWhiteSpace(namespaceToken) ? null : namespaceToken.ToLowerInvariant();
    }

    /// <summary>
    /// Extracts tool name from first H1 heading in content.
    /// </summary>
    private string? ExtractToolName(string content)
    {
        var match = ToolNameRegex.Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    private string? ExtractCommand(string content)
    {
        var match = CommandRegex.Match(content);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    /// <summary>
    /// Extracts the first line of tool description from content.
    /// Skips @mcpcli comment and captures the first descriptive line.
    /// </summary>
    private string? ExtractDescription(string content)
    {
        var lines = content.Split('\n');
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            // Skip comment lines and empty lines
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("<!--") || trimmed.StartsWith("#"))
            {
                continue;
            }
            
            // Return first non-comment, non-heading line
            return trimmed;
        }
        
        return null;
    }

    /// <summary>
    /// Strips YAML frontmatter from markdown content.
    /// </summary>
    private string StripFrontmatter(string content)
    {
        return FrontmatterRegex.Replace(content, string.Empty);
    }
}
