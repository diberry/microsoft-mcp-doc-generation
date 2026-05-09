// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DocGeneration.Steps.ToolFamilyCleanup.Services;

/// <summary>
/// Validates CLI tab structure in family articles.
/// </summary>
public static class CliTabValidator
{
    public record ValidationResult(
        bool IsValid,
        IReadOnlyList<string> Errors,
        IReadOnlyList<string> Warnings);

    /// <summary>
    /// Validates tab structure in a family article.
    /// </summary>
    public static ValidationResult Validate(string markdown)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var lines = markdown.Split('\n');

        int mcpTabCount = 0;
        int cliTabCount = 0;
        int tabTerminatorCount = 0;
        bool inTabGroup = false;
        int lineNum = 0;

        foreach (var rawLine in lines)
        {
            lineNum++;
            var line = rawLine.TrimEnd('\r').Trim();

            if (line == "#### [MCP Server](#tab/mcp-server)")
            {
                if (inTabGroup)
                    errors.Add($"Line {lineNum}: Nested tab group detected (MCP Server tab opened inside existing tab group).");
                mcpTabCount++;
                inTabGroup = true;
            }
            else if (line == "#### [CLI](#tab/cli)")
            {
                if (!inTabGroup)
                    errors.Add($"Line {lineNum}: CLI tab opened without preceding MCP Server tab.");
                cliTabCount++;
            }
            else if (line == "---" && inTabGroup)
            {
                tabTerminatorCount++;
                inTabGroup = false;
            }
        }

        if (mcpTabCount != cliTabCount)
            errors.Add($"Mismatched tab counts: {mcpTabCount} MCP Server tabs vs {cliTabCount} CLI tabs.");

        if (mcpTabCount != tabTerminatorCount)
            errors.Add($"Mismatched tab terminators: {mcpTabCount} tab groups opened but {tabTerminatorCount} terminated with ---.");

        if (inTabGroup)
            errors.Add("Unterminated tab group at end of document.");

        if (mcpTabCount == 0)
            warnings.Add("No CLI tabs found in article.");

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }
}
