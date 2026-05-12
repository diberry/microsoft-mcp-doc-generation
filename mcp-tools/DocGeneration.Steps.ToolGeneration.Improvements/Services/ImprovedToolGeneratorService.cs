// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using GenerativeAI;
using System.Linq;
using System.Text.RegularExpressions;
using ToolGeneration_Improved.Models;
using System.Text;

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
    /// Also detects leaked frozen section tokens (<<<FROZEN_SECTION_N>>>) and frozen param table tokens (<<<FROZEN_PARAM_TABLE_N>>>).
    /// </summary>
    private static readonly System.Text.RegularExpressions.Regex LeakedTokenRegex = new(
        @"(<<<TPL_LABEL_\d+>>>|__TPL_LABEL_\d+__|\*\*TPL_LABEL_\d+\*\*|<<<FROZEN_SECTION_\d+>>>|<<<FROZEN_PARAM_TABLE_\d+>>>)",
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
    /// <summary>
    /// Default per-tool AI call timeout. Each tool gets this much time before falling back to composed content.
    /// </summary>
    internal static readonly TimeSpan DefaultPerToolTimeout = TimeSpan.FromMinutes(5);

    public async Task<int> GenerateImprovedToolFilesAsync(
        string composedToolsDir,
        string outputDir,
        int maxTokens = 8000,
        TimeSpan? perToolTimeout = null,
        CancellationToken pipelineCancellationToken = default)
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

        var timeout = perToolTimeout ?? DefaultPerToolTimeout;

        Console.WriteLine($"  Composed Tools Directory: {composedToolsDir}");
        Console.WriteLine($"  Output Directory: {outputDir}");
        Console.WriteLine($"  Max Tokens: {maxTokens}");
        Console.WriteLine($"  Per-Tool Timeout: {timeout.TotalMinutes:F0} minutes");
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
        int timedOutCount = 0;

        for (int i = 0; i < composedFiles.Length; i++)
        {
            var composedFilePath = composedFiles[i];
            var fileName = Path.GetFileName(composedFilePath);
            var progress = $"[{i + 1}/{composedFiles.Length}]";

            // Check if pipeline cancellation was requested before starting this tool
            pipelineCancellationToken.ThrowIfCancellationRequested();

            Console.Write($"  {progress} Processing {fileName}...");

            // Load composed file content (outside try — file read failures are hard errors)
            var originalContent = await File.ReadAllTextAsync(composedFilePath, pipelineCancellationToken);

            // Pre-processing (deterministic, should not fail — errors here are real bugs)
            var requiredParameters = ExtractRequiredParameters(originalContent);
            var frozenContent = ProtectExamplePromptSections(originalContent, out var sectionMap);
            frozenContent = ProtectParameterTable(frozenContent, out var paramTableMap);
            var mcpCliComment = ExtractMcpCliComment(originalContent);
            var protectedContent = ProtectTemplateLabels(frozenContent, out var labelMap);
            var userPrompt = string.Format(_userPromptTemplate, protectedContent);
            userPrompt = AppendRequiredParameterPreservationInstruction(userPrompt, requiredParameters);

            // AI call — this is the section that may hang/fail/timeout
            string improvedContent;
            try
            {
                using var toolCts = CancellationTokenSource.CreateLinkedTokenSource(pipelineCancellationToken);
                toolCts.CancelAfter(timeout);
                var toolCt = toolCts.Token;

                improvedContent = await _aiClient.GetChatCompletionAsync(
                    _systemPrompt,
                    userPrompt,
                    maxTokens,
                    toolCt);
            }
            catch (OperationCanceledException) when (pipelineCancellationToken.IsCancellationRequested)
            {
                // External (caller) cancellation — propagate immediately
                Console.WriteLine($" ✗ Cancelled by caller");
                throw;
            }
            catch (OperationCanceledException)
            {
                // Per-tool timeout — save original composed content as fallback
                Console.WriteLine($" ⏱ Timed out after {timeout.TotalMinutes:F0}m — saving original");
                timedOutCount++;
                await SaveFallbackContent(originalContent, outputDir, fileName);
                continue;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("truncated"))
            {
                Console.WriteLine($" ⚠ Truncated - saving original");
                Console.WriteLine($"      {ex.Message}");
                skippedCount++;
                await SaveFallbackContent(originalContent, outputDir, fileName);
                continue;
            }
            catch (Exception ex)
            {
                // AI-specific failure (network, server error, etc.) — save original as fallback
                Console.WriteLine($" ⚠ AI error — saving original");
                Console.WriteLine($"      {ex.GetType().Name}: {ex.Message}");
                skippedCount++;
                await SaveFallbackContent(originalContent, outputDir, fileName);
                continue;
            }

            // Post-processing (deterministic — errors here are real bugs, not swallowed)
            var restoredContent = RestoreTemplateLabels(improvedContent, labelMap);
            restoredContent = NormalizeTemplateLabels(restoredContent);
            restoredContent = RestoreExamplePromptSections(restoredContent, sectionMap);
            restoredContent = RestoreParameterTable(restoredContent, paramTableMap);
            restoredContent = RestoreMcpCliComment(restoredContent, mcpCliComment);

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
            await File.WriteAllTextAsync(outputPath, restoredContent, Encoding.UTF8);

            successCount++;
            Console.WriteLine(" ✓");

            // Add a small delay between requests to avoid rate limiting
            if (i < composedFiles.Length - 1)
            {
                await Task.Delay(100, CancellationToken.None);
            }
        }

        Console.WriteLine();
        Console.WriteLine($"  ✓ Successfully improved {successCount} tool files");
        
        if (timedOutCount > 0)
        {
            Console.WriteLine($"  ⏱ Timed out {timedOutCount} files (original saved)");
        }

        if (skippedCount > 0)
        {
            Console.WriteLine($"  ⚠ Skipped {skippedCount} files (AI error/truncation — original saved)");
        }
        
        if (errorCount > 0)
        {
            Console.WriteLine($"  ✗ Failed to process {errorCount} files");
        }

        return errorCount > 0 ? 1 : 0;
    }

    /// <summary>
    /// Saves the original composed content as a fallback when AI improvement fails.
    /// Uses already-loaded content to avoid re-reading the file.
    /// </summary>
    private static async Task SaveFallbackContent(string originalContent, string outputDir, string fileName)
    {
        try
        {
            var outputPath = Path.Combine(outputDir, fileName);
            await File.WriteAllTextAsync(outputPath, originalContent, Encoding.UTF8);
        }
        catch (Exception saveEx)
        {
            Console.WriteLine($"      Error saving fallback: {saveEx.Message}");
        }
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

    /// <summary>
    /// Replaces every parameter table block in <paramref name="content"/> with a
    /// <c>&lt;&lt;&lt;FROZEN_PARAM_TABLE_N&gt;&gt;&gt;</c> token before the AI call so the AI
    /// cannot alter Required/Optional values or rename parameter display names (#554, #558).
    /// The original table text (header + divider + data rows + optional footnote) is stored in
    /// <paramref name="tableMap"/> for restoration after the AI returns.
    /// Supports multiple parameter tables in a single composed file.
    /// </summary>
    internal static string ProtectParameterTable(string content, out Dictionary<string, string> tableMap)
    {
        var map = new Dictionary<string, string>();
        if (string.IsNullOrEmpty(content))
        {
            tableMap = map;
            return content;
        }

        var newline = DetectNewLine(content);
        var lines = SplitLines(content).ToList();
        var resultLines = new List<string>();
        var index = 0;
        var i = 0;

        while (i < lines.Count)
        {
            if (IsParameterTableHeader(lines[i]))
            {
                var capturedLines = new List<string> { lines[i] };
                var j = i + 1;

                // Capture divider row + all contiguous pipe-delimited data rows
                while (j < lines.Count && lines[j].TrimStart().StartsWith("|", StringComparison.Ordinal))
                {
                    capturedLines.Add(lines[j]);
                    j++;
                }

                // Capture optional conditional-required footnote (* footnote).
                // Allow an optional blank line immediately before the footnote line.
                if (j < lines.Count)
                {
                    var nextLine = lines[j];
                    if (string.IsNullOrWhiteSpace(nextLine) &&
                        j + 1 < lines.Count &&
                        lines[j + 1].TrimStart().StartsWith("*", StringComparison.Ordinal))
                    {
                        capturedLines.Add(lines[j]);       // blank separator
                        capturedLines.Add(lines[j + 1]);   // footnote
                        j += 2;
                    }
                    else if (nextLine.TrimStart().StartsWith("*", StringComparison.Ordinal))
                    {
                        capturedLines.Add(nextLine);
                        j++;
                    }
                }

                var token = $"<<<FROZEN_PARAM_TABLE_{index++}>>>";
                map[token] = string.Join(newline, capturedLines);
                resultLines.Add(token);
                i = j;
            }
            else
            {
                resultLines.Add(lines[i]);
                i++;
            }
        }

        tableMap = map;
        return string.Join(newline, resultLines);
    }

    /// <summary>
    /// Restores parameter table blocks that were frozen by <see cref="ProtectParameterTable"/>.
    /// </summary>
    internal static string RestoreParameterTable(string content, Dictionary<string, string> tableMap)
    {
        if (string.IsNullOrEmpty(content) || tableMap.Count == 0)
        {
            return content;
        }

        var restored = content;
        foreach (var pair in tableMap)
        {
            restored = restored.Replace(pair.Key, pair.Value);
        }

        return restored;
    }

    private static readonly Regex McpCliCommentRegex = new(@"<!--\s*@mcpcli\s+[^>]+-->", RegexOptions.Compiled);

    /// <summary>
    /// Extracts the @mcpcli command comment from tool content.
    /// These comments are used for validation during content PRs and must be preserved.
    /// </summary>
    internal static string? ExtractMcpCliComment(string content)
    {
        var match = McpCliCommentRegex.Match(content);
        return match.Success ? match.Value : null;
    }

    /// <summary>
    /// Restores the @mcpcli command comment if the AI stripped it.
    /// Inserts after the first H1 heading if missing.
    /// </summary>
    internal static string RestoreMcpCliComment(string content, string? mcpCliComment)
    {
        if (string.IsNullOrEmpty(mcpCliComment) || string.IsNullOrEmpty(content))
        {
            return content;
        }

        // Already present — no action needed
        if (content.Contains("@mcpcli", StringComparison.Ordinal))
        {
            return content;
        }

        // Insert after first H1 heading (# ...)
        var lines = content.Split('\n').ToList();
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimStart().StartsWith("# ") && !lines[i].TrimStart().StartsWith("## "))
            {
                lines.Insert(i + 1, "");
                lines.Insert(i + 2, mcpCliComment);
                return string.Join('\n', lines);
            }
        }

        // Fallback: insert at beginning (after frontmatter if present)
        var fmEnd = content.IndexOf("---\n", content.IndexOf("---\n") + 4);
        if (fmEnd > 0)
        {
            return content.Insert(fmEnd + 4, $"\n{mcpCliComment}\n");
        }

        return $"{mcpCliComment}\n{content}";
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
