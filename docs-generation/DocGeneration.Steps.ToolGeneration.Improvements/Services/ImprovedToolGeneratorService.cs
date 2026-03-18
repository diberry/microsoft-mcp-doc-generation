// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using System.Linq;
using ToolGeneration_Improved.Models;

namespace ToolGeneration_Improved.Services;

/// <summary>
/// Service that improves tool documentation using AI based on Microsoft content guidelines
/// </summary>
public class ImprovedToolGeneratorService
{
    internal sealed record RequiredParameterEntry(string DisplayName, string NormalizedName, string RowLine);

    private sealed record ParameterTableRow(int LineIndex, string DisplayName, string NormalizedName, string RequirementText, string OriginalLine);

    private sealed record ParameterTable(int HeaderLineIndex, int EndLineIndexExclusive, List<string> Lines, List<ParameterTableRow> Rows);

    private readonly GenerativeAIClient _aiClient;
    private readonly string _systemPrompt;
    private readonly string _userPromptTemplate;
    internal static readonly string[] TemplateLabels =
    [
        "Example prompts include:",
        "Example prompts:",
        "Required options:",
        "Optional options:",
        "Required parameters:",
        "Optional parameters:",
        "**Prerequisites**:",
        "**Success verification**:",
        "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):",
        "[Tool annotation hints](../index.md#tool-annotations-for-azure-mcp-server):",
        "[Tool annotation hints](../../index.md#tool-annotations-for-azure-mcp-server):"
    ];

    private static readonly string[] ParameterSectionMarkers =
    [
        "[Tool annotation hints](index.md#tool-annotations-for-azure-mcp-server):",
        "[Tool annotation hints](../index.md#tool-annotations-for-azure-mcp-server):",
        "[Tool annotation hints](../../index.md#tool-annotations-for-azure-mcp-server):",
        "**Prerequisites**:",
        "**Success verification**:"
    ];

    /// <summary>
    /// Regex that detects leaked placeholder tokens in output content.
    /// Matches the current format (<<<TPL_LABEL_N>>>) and the old format (__TPL_LABEL_N__ or **TPL_LABEL_N**).
    /// Also detects leaked frozen section tokens (<<<FROZEN_SECTION_N>>>).
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex LeakedTokenRegex = new(
        @"(<<<TPL_LABEL_\d+>>>|__TPL_LABEL_\d+__|\*\*TPL_LABEL_\d+\*\*|<<<FROZEN_SECTION_\d+>>>)",
        System.Text.RegularExpressions.RegexOptions.Compiled);

    public ImprovedToolGeneratorService(GenerativeAIClient aiClient, string systemPrompt, string userPromptTemplate)
    {
        _aiClient = aiClient;
        _systemPrompt = systemPrompt;
        _userPromptTemplate = userPromptTemplate;
    }

    /// <summary>
    /// Generates improved tool files using AI to apply Microsoft content guidelines
    /// </summary>
    public async Task<int> GenerateImprovedToolFilesAsync(
        string composedToolsDir,
        string outputDir,
        int maxTokens = 8000)
    {
        Console.WriteLine("\n┌─────────────────────────────────────────────┐");
        Console.WriteLine("│  Generating AI-Improved Tool Files         │");
        Console.WriteLine("└─────────────────────────────────────────────┘");

        // Validate input directory exists
        if (!Directory.Exists(composedToolsDir))
        {
            Console.Error.WriteLine($"Error: Composed tools directory not found: {composedToolsDir}");
            return 1;
        }

        Console.WriteLine($"  Composed Tools Directory: {composedToolsDir}");
        Console.WriteLine($"  Output Directory: {outputDir}");
        Console.WriteLine($"  Max Tokens: {maxTokens}");
        Console.WriteLine();

        // Ensure output directory exists
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            Console.WriteLine($"  Created output directory: {outputDir}");
        }

        // Get all composed tool files
        var composedFiles = Directory.GetFiles(composedToolsDir, "*.md");
        if (composedFiles.Length == 0)
        {
            Console.WriteLine("  No composed tool files found.");
            return 0;
        }

        Console.WriteLine($"  Found {composedFiles.Length} composed tool files to improve");
        Console.WriteLine();

        int successCount = 0;
        int skippedCount = 0;
        int errorCount = 0;

        for (int i = 0; i < composedFiles.Length; i++)
        {
            var composedFilePath = composedFiles[i];
            var fileName = Path.GetFileName(composedFilePath);
            var progress = $"[{i + 1}/{composedFiles.Length}]";

            try
            {
                Console.Write($"  {progress} Processing {fileName}...");

                // Load composed file content
                var originalContent = await File.ReadAllTextAsync(composedFilePath);
                var requiredParameters = ExtractRequiredParameters(originalContent);

                // Freeze example prompt sections before any other protection
                var frozenContent = ProtectExamplePromptSections(originalContent, out var sectionMap);

                // Protect handlebar template labels from AI modification
                var protectedContent = ProtectTemplateLabels(frozenContent, out var labelMap);

                // Generate user prompt with the content
                var userPrompt = string.Format(_userPromptTemplate, protectedContent);
                userPrompt = AppendRequiredParameterPreservationInstruction(userPrompt, requiredParameters);

                // Call AI to improve the content
                var improvedContent = await _aiClient.GetChatCompletionAsync(
                    _systemPrompt,
                    userPrompt,
                    maxTokens);

                // Restore protected labels and normalize formatting
                var restoredContent = RestoreTemplateLabels(improvedContent, labelMap);
                restoredContent = NormalizeTemplateLabels(restoredContent);

                // Restore frozen example prompt sections
                restoredContent = RestoreExamplePromptSections(restoredContent, sectionMap);

                // Validate no leaked placeholder tokens remain
                var leakedTokens = ValidateRestoredContent(restoredContent);
                if (leakedTokens.Count > 0)
                {
                    Console.WriteLine($" ⚠ Leaked tokens detected: {string.Join(", ", leakedTokens)}");
                    Console.WriteLine($"      Falling back to original content");
                    restoredContent = originalContent;
                }
                else if (requiredParameters.Count > 0)
                {
                    var missingRequiredParameters = FindMissingRequiredParameters(restoredContent, requiredParameters);
                    if (missingRequiredParameters.Count > 0)
                    {
                        Console.WriteLine($" ⚠ Missing required parameters after AI: {string.Join(", ", missingRequiredParameters.Select(parameter => parameter.DisplayName))}");
                        restoredContent = ReinjectMissingRequiredParameters(restoredContent, originalContent, missingRequiredParameters);

                        var remainingMissingParameters = FindMissingRequiredParameters(restoredContent, requiredParameters);
                        if (remainingMissingParameters.Count > 0)
                        {
                            Console.WriteLine($"      Reinjection incomplete; falling back to original content");
                            restoredContent = originalContent;
                        }
                    }
                }

                // Save improved content
                var outputPath = Path.Combine(outputDir, fileName);
                await File.WriteAllTextAsync(outputPath, restoredContent);

                successCount++;
                Console.WriteLine(" ✓");
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("truncated"))
            {
                // Handle truncation error - save original instead
                Console.WriteLine($" ⚠ Truncated - saving original");
                Console.WriteLine($"      {ex.Message}");
                
                try
                {
                    var originalContent = await File.ReadAllTextAsync(composedFilePath);
                    var outputPath = Path.Combine(outputDir, fileName);
                    await File.WriteAllTextAsync(outputPath, originalContent);
                    skippedCount++;
                }
                catch (Exception saveEx)
                {
                    Console.WriteLine($"      Error saving original: {saveEx.Message}");
                    errorCount++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" ✗");
                Console.WriteLine($"      Error: {ex.Message}");
                errorCount++;
            }

            // Add a small delay between requests to avoid rate limiting
            if (i < composedFiles.Length - 1)
            {
                await Task.Delay(100);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  ✓ Successfully improved {successCount} tool files");
        
        if (skippedCount > 0)
        {
            Console.WriteLine($"  ⚠ Skipped {skippedCount} files (truncation - original saved)");
        }
        
        if (errorCount > 0)
        {
            Console.WriteLine($"  ✗ Failed to process {errorCount} files");
        }

        return errorCount > 0 ? 1 : 0;
    }

    /// <summary>
    /// Validates that no placeholder tokens leaked into the restored content.
    /// Returns a list of leaked token strings found (empty if valid).
    /// </summary>
    internal static List<string> ValidateRestoredContent(string content)
    {
        var leaked = new List<string>();
        if (string.IsNullOrEmpty(content)) return leaked;

        foreach (System.Text.RegularExpressions.Match match in LeakedTokenRegex.Matches(content))
        {
            leaked.Add(match.Value);
        }

        return leaked;
    }

    internal static List<RequiredParameterEntry> ExtractRequiredParameters(string content)
    {
        var table = FindParameterTable(content);
        if (table is null)
        {
            return [];
        }

        return table.Rows
            .Where(row => row.RequirementText.StartsWith("Required", StringComparison.OrdinalIgnoreCase))
            .GroupBy(row => row.NormalizedName)
            .Select(group => new RequiredParameterEntry(
                group.Key,
                group.Key,
                group.First().OriginalLine))
            .ToList();
    }

    internal static string AppendRequiredParameterPreservationInstruction(string userPrompt, IReadOnlyList<RequiredParameterEntry> requiredParameters)
    {
        if (requiredParameters.Count == 0)
        {
            return userPrompt;
        }

        var requiredNames = string.Join(", ", requiredParameters.Select(parameter => $"`{parameter.DisplayName}`"));
        return $"{userPrompt}\n\nCRITICAL REQUIREMENT: Preserve every required parameter row from the input parameter table in the final output. The final output must continue to include these required parameters: {requiredNames}.";
    }

    internal static List<RequiredParameterEntry> FindMissingRequiredParameters(string content, IReadOnlyList<RequiredParameterEntry> requiredParameters)
    {
        if (requiredParameters.Count == 0)
        {
            return [];
        }

        var table = FindParameterTable(content);
        if (table is null)
        {
            return requiredParameters.ToList();
        }

        var presentParameters = table.Rows
            .Select(row => row.NormalizedName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return requiredParameters
            .Where(parameter => !presentParameters.Contains(parameter.NormalizedName))
            .ToList();
    }

    internal static string ReinjectMissingRequiredParameters(string content, string originalContent, IReadOnlyList<RequiredParameterEntry> missingParameters)
    {
        if (missingParameters.Count == 0)
        {
            return content;
        }

        var originalTable = FindParameterTable(originalContent);
        if (originalTable is null)
        {
            return content;
        }

        var newline = DetectNewLine(content);
        var currentTable = FindParameterTable(content);
        if (currentTable is null)
        {
            var tableBlock = string.Join(newline, originalTable.Lines).TrimEnd();
            return InsertParameterTableBlock(content, tableBlock, newline);
        }

        var repairedLines = SplitLines(content).ToList();
        var missingLines = missingParameters
            .Select(parameter => parameter.RowLine)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (missingLines.Count == 0)
        {
            return content;
        }

        var firstOptionalRow = currentTable.Rows.FirstOrDefault(row =>
            !row.RequirementText.StartsWith("Required", StringComparison.OrdinalIgnoreCase));
        var insertAt = firstOptionalRow?.LineIndex ?? currentTable.EndLineIndexExclusive;
        repairedLines.InsertRange(insertAt, missingLines);
        return string.Join(newline, repairedLines);
    }

    internal static string ProtectTemplateLabels(string content, out Dictionary<string, string> labelMap)
    {
        var map = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(content))
        {
            labelMap = map;
            return content;
        }

        var labelPattern = string.Join("|", TemplateLabels.Select(label => System.Text.RegularExpressions.Regex.Escape(label)));
        var regex = new System.Text.RegularExpressions.Regex(
            $@"^(\s*)({labelPattern})\s*\r?\n",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        var index = 0;
        var protectedContent = regex.Replace(content, match =>
        {
            // Use angle-bracket fences so the AI won't reformat the token.
            // Double-underscore (__x__) gets interpreted as markdown bold (**x**).
            var token = $"<<<TPL_LABEL_{index++}>>>";
            map[token] = match.Value;
            return token;
        });

        labelMap = map;
        return protectedContent;
    }

    internal static string RestoreTemplateLabels(string content, Dictionary<string, string> labelMap)
    {
        if (string.IsNullOrEmpty(content) || labelMap.Count == 0)
        {
            return content;
        }

        var restored = content;
        foreach (var pair in labelMap)
        {
            restored = restored.Replace(pair.Key, pair.Value);
        }

        return restored;
    }

    private static readonly System.Text.RegularExpressions.Regex ExamplePromptSectionRegex = new(
        @"^([ \t]*Example prompts(?:\s+include)?:\s*\r?\n(?:[ \t]*- .+\r?\n?)+)",
        System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.Compiled);

    internal static string ProtectExamplePromptSections(string content, out Dictionary<string, string> sectionMap)
    {
        var map = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(content))
        {
            sectionMap = map;
            return content;
        }

        var index = 0;
        var protectedContent = ExamplePromptSectionRegex.Replace(content, match =>
        {
            var token = $"<<<FROZEN_SECTION_{index++}>>>";
            map[token] = match.Value;
            return token;
        });

        sectionMap = map;
        return protectedContent;
    }

    internal static string RestoreExamplePromptSections(string content, Dictionary<string, string> sectionMap)
    {
        if (string.IsNullOrEmpty(content) || sectionMap.Count == 0)
        {
            return content;
        }

        var restored = content;
        foreach (var pair in sectionMap)
        {
            restored = restored.Replace(pair.Key, pair.Value);
        }

        return restored;
    }

    internal static string NormalizeTemplateLabels(string content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return content;
        }

        var normalized = content;
        foreach (var label in TemplateLabels)
        {
            var labelText = label.Trim();
            var labelLiteral = System.Text.RegularExpressions.Regex.Escape(labelText.Trim('*'));
            var regex = new System.Text.RegularExpressions.Regex(
                $@"^(\s*)(\*\*|###\s+)?{labelLiteral}(\*\*)?\s*$",
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            normalized = regex.Replace(normalized, $"$1{labelText}");
        }

        return normalized;
    }

    private static ParameterTable? FindParameterTable(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var lines = SplitLines(content);
        for (var i = 0; i < lines.Count; i++)
        {
            if (!IsParameterTableHeader(lines[i]))
            {
                continue;
            }

            var tableLines = new List<string> { lines[i] };
            var rows = new List<ParameterTableRow>();
            var lineIndex = i + 1;
            while (lineIndex < lines.Count && lines[lineIndex].TrimStart().StartsWith("|", StringComparison.Ordinal))
            {
                var line = lines[lineIndex];
                tableLines.Add(line);

                var cells = ParseMarkdownTableCells(line);
                if (cells.Count >= 3 && !IsMarkdownDividerRow(cells))
                {
                    var displayName = cells[0];
                    var normalizedName = NormalizeParameterName(displayName);
                    if (!string.IsNullOrWhiteSpace(normalizedName))
                    {
                        rows.Add(new ParameterTableRow(
                            lineIndex,
                            displayName,
                            normalizedName,
                            cells[1],
                            line));
                    }
                }

                lineIndex++;
            }

            return new ParameterTable(i, lineIndex, tableLines, rows);
        }

        return null;
    }

    private static bool IsParameterTableHeader(string line)
    {
        var cells = ParseMarkdownTableCells(line);
        if (cells.Count < 3)
        {
            return false;
        }

        var firstHeader = NormalizeParameterName(cells[0]);
        return (firstHeader.Equals("parameter", StringComparison.OrdinalIgnoreCase) ||
                firstHeader.Equals("option", StringComparison.OrdinalIgnoreCase)) &&
               cells[1].Contains("Required or optional", StringComparison.OrdinalIgnoreCase) &&
               cells[2].Contains("Description", StringComparison.OrdinalIgnoreCase);
    }

    private static List<string> ParseMarkdownTableCells(string line)
    {
        var trimmedLine = line.Trim();
        if (!trimmedLine.StartsWith("|", StringComparison.Ordinal) ||
            !trimmedLine.EndsWith("|", StringComparison.Ordinal))
        {
            return [];
        }

        var segments = trimmedLine.Split('|');
        return segments.Skip(1).Take(segments.Length - 2).Select(segment => segment.Trim()).ToList();
    }

    private static bool IsMarkdownDividerRow(IReadOnlyList<string> cells)
    {
        return cells.All(cell =>
            !string.IsNullOrWhiteSpace(cell) &&
            cell.All(ch => ch == '-' || ch == ':' || char.IsWhiteSpace(ch)));
    }

    private static string NormalizeParameterName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = System.Text.RegularExpressions.Regex.Replace(value, @"\[([^\]]+)\]\([^)]+\)", "$1");
        normalized = normalized.Replace("`", string.Empty)
            .Replace("*", string.Empty)
            .Replace("_", string.Empty)
            .Trim();
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\s+", " ");
        return normalized;
    }

    private static IReadOnlyList<string> SplitLines(string content)
    {
        return content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
    }

    private static string DetectNewLine(string content)
    {
        return content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
    }

    private static string InsertParameterTableBlock(string content, string tableBlock, string newline)
    {
        foreach (var marker in ParameterSectionMarkers)
        {
            var markerIndex = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                var prefix = content[..markerIndex].TrimEnd('\r', '\n');
                var suffix = content[markerIndex..].TrimStart('\r', '\n');
                return $"{prefix}{newline}{newline}{tableBlock}{newline}{newline}{suffix}";
            }
        }

        return $"{content.TrimEnd('\r', '\n')}{newline}{newline}{tableBlock}{newline}";
    }
}
