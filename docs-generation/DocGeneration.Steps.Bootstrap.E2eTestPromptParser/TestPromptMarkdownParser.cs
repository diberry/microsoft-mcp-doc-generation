using System.Text.RegularExpressions;
using E2eTestPromptParser.Models;

namespace E2eTestPromptParser;

/// <summary>
/// Parses the e2eTestPrompts.md markdown file into a structured <see cref="E2eTestPromptDocument"/>.
/// </summary>
public static partial class TestPromptMarkdownParser
{
    /// <summary>
    /// Parse markdown content from a string.
    /// </summary>
    public static E2eTestPromptDocument Parse(string markdownContent)
    {
        ArgumentNullException.ThrowIfNull(markdownContent);

        var lines = markdownContent.Split('\n');
        return ParseLines(lines);
    }

    /// <summary>
    /// Parse markdown content from a file path.
    /// </summary>
    public static E2eTestPromptDocument ParseFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return Parse(content);
    }

    /// <summary>
    /// Parse markdown content from a stream.
    /// </summary>
    public static async Task<E2eTestPromptDocument> ParseStreamAsync(Stream stream)
    {
        using var reader = new StreamReader(stream);
        var content = await reader.ReadToEndAsync();
        return Parse(content);
    }

    private static E2eTestPromptDocument ParseLines(string[] lines)
    {
        string title = string.Empty;
        var sections = new List<ServiceAreaSection>();
        string? currentHeading = null;
        var currentEntries = new List<TestPromptEntry>();
        bool inTable = false;
        int headerRowsSeen = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].TrimEnd('\r');

            // H1 title
            if (line.StartsWith("# ", StringComparison.Ordinal) && !line.StartsWith("## ", StringComparison.Ordinal))
            {
                title = line[2..].Trim();
                continue;
            }

            // H2 section heading
            if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                // Save previous section
                if (currentHeading is not null)
                {
                    sections.Add(new ServiceAreaSection(currentHeading, currentEntries.ToList()));
                    currentEntries.Clear();
                }

                currentHeading = line[3..].Trim();
                inTable = false;
                headerRowsSeen = 0;
                continue;
            }

            // Skip empty lines
            if (string.IsNullOrWhiteSpace(line))
            {
                // Reset table state on blank line (paragraph break)
                if (!inTable)
                {
                    headerRowsSeen = 0;
                }
                continue;
            }

            // We're inside a section — look for table rows
            if (currentHeading is null)
                continue;

            // Detect table lines (start with |)
            if (line.TrimStart().StartsWith('|'))
            {
                // Separator row (|:---|:---|)
                if (SeparatorRowRegex().IsMatch(line))
                {
                    headerRowsSeen++;
                    inTable = true;
                    continue;
                }

                // Header row (| Tool Name | Test Prompt |)
                if (!inTable)
                {
                    headerRowsSeen++;
                    continue;
                }

                // Data row
                if (inTable)
                {
                    var entry = ParseTableRow(line);
                    if (entry is not null)
                    {
                        currentEntries.Add(entry);
                    }
                }
            }
        }

        // Save final section
        if (currentHeading is not null)
        {
            sections.Add(new ServiceAreaSection(currentHeading, currentEntries.ToList()));
        }

        return new E2eTestPromptDocument
        {
            Title = title,
            Sections = sections
        };
    }

    private static TestPromptEntry? ParseTableRow(string line)
    {
        // Split by | and trim — expected: empty, toolName, testPrompt, empty
        var cells = line.Split('|');

        // Need at least 3 segments (empty + toolName + testPrompt from "| x | y |")
        if (cells.Length < 3)
            return null;

        // Find the first two non-empty cells
        string? toolName = null;
        string? testPrompt = null;

        foreach (var cell in cells)
        {
            var trimmed = cell.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            if (toolName is null)
            {
                toolName = trimmed;
            }
            else if (testPrompt is null)
            {
                testPrompt = trimmed;
                break;
            }
        }

        if (toolName is null || testPrompt is null)
            return null;

        // Clean up escaped angle brackets: \<value> → <value>
        testPrompt = testPrompt.Replace("\\<", "<", StringComparison.Ordinal);
        testPrompt = testPrompt.Replace("\\>", ">", StringComparison.Ordinal);

        return new TestPromptEntry(toolName, testPrompt);
    }

    [GeneratedRegex(@"^\s*\|[\s:]*-+[\s:]*\|")]
    private static partial Regex SeparatorRowRegex();
}
