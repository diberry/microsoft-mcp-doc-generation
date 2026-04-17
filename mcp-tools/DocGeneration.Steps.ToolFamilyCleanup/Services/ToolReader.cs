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

        // Sort tools within each family by resource type first, then by verb (#279).
        // Command format: "namespace resource [resource2…] verb"
        // Sort key groups all operations on the same resource together.
        foreach (var family in toolsByFamily.Keys)
        {
            toolsByFamily[family] = toolsByFamily[family]
                .OrderBy(t => GetResourceSortKey(t.Command), StringComparer.Ordinal)
                .ThenBy(t => t.ToolName, StringComparer.Ordinal)
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
            SourceFilePath = filePath,
            ResourceType = GetResourceType(command)
        };
    }

    /// <summary>
    /// Extracts family name from filename using brand mapping data.
    /// Pattern: {family}-*.md
    /// Examples:
    ///   advisor-recommendation-list.md → advisor
    ///   storage-account-create.md → storage
    ///   ai-foundry-agents-connect.md → ai-foundry (special case)
    ///   azure-cosmos-db-list.md → cosmos (mapped via brand-to-server-mapping.json)
    /// </summary>
    private string ExtractFamilyNameFromFileName(string fileName)
    {
        // Remove .md extension
        var nameWithoutExt = fileName.Replace(".md", "");
        
        // Try to match against brand mappings first (best accuracy)
        var brandMappings = Shared.DataFileLoader.LoadBrandMappingsAsync().GetAwaiter().GetResult();
        foreach (var mapping in brandMappings.Values)
        {
            var prefix = mapping.FileName?.ToLowerInvariant();
            if (!string.IsNullOrEmpty(prefix))
            {
                // Check if filename starts with this prefix
                if (nameWithoutExt.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                    nameWithoutExt.StartsWith($"{prefix}-", StringComparison.OrdinalIgnoreCase))
                {
                    return mapping.McpServerName;
                }
            }
        }
        
        // Fallback: split by hyphens and use heuristics
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
        
        // Special case for "azure-*" prefixes - skip "azure" and try to match the service name
        if (parts[0] == "azure" && parts.Length > 1)
        {
            // azure-cosmos-db-... → cosmos (if we can find it in mappings by trying 2+ segments)
            for (int segmentCount = Math.Min(parts.Length - 1, 4); segmentCount >= 1; segmentCount--)
            {
                var candidatePrefix = string.Join("-", parts.Take(1 + segmentCount));
                foreach (var mapping in brandMappings.Values)
                {
                    if (string.Equals(mapping.FileName, candidatePrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        return mapping.McpServerName;
                    }
                }
            }
            
            // If no mapping found, return second segment as a fallback
            return parts[1];
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
    private static string StripFrontmatter(string content) =>
        Shared.FrontmatterUtility.StripFrontmatter(content)!;

    /// <summary>
    /// Extracts the resource sub-type from a command string.
    /// Command format: "namespace resource1 [resource2...] verb"
    /// Returns the resource portion (e.g., "disk" from "compute disk create",
    /// "agent thread" from "foundry agent thread create").
    /// Returns empty string for two-segment commands (namespace + verb only).
    /// </summary>
    internal static string GetResourceType(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return string.Empty;

        var segments = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Two or fewer segments: no resource sub-type
        if (segments.Length <= 2)
            return string.Empty;

        // Three+ segments: "namespace resource... verb" -> resource portion
        return string.Join(" ", segments[1..^1]);
    }

    /// <summary>
    /// Builds a sort key from a command that groups by resource type first, then by verb.
    /// Command format: "namespace resource1 [resource2…] verb"
    /// Result format:  "resource1 resource2\0verb"
    /// The NUL separator ensures resource groups sort together regardless of verb names.
    /// For two-segment commands (namespace + verb), returns "\0verb" so bare verbs sort
    /// before any resource-specific tools within the same family.
    /// </summary>
    internal static string GetResourceSortKey(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return string.Empty;

        var segments = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length <= 1)
            return command;

        // Two-segment: "namespace verb" → sort by "\0verb" (bare verbs first)
        if (segments.Length == 2)
            return $"\0{segments[1]}";

        // Three+ segments: "namespace resource… verb"
        var resource = string.Join(" ", segments[1..^1]);
        var verb = segments[^1];
        return $"{resource}\0{verb}";
    }
}
