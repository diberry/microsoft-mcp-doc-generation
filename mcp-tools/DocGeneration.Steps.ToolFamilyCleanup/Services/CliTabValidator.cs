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
        bool cliTabSeenInCurrentGroup = false;
        bool mcpTabSeenInCurrentGroup = false;
        int lineNum = 0;

        foreach (var rawLine in lines)
        {
            lineNum++;
            var line = rawLine.TrimEnd('\r').Trim();

            if (line == "#### [Azure MCP CLI](#tab/azure-mcp-cli)")
            {
                if (inTabGroup)
                    errors.Add($"Line {lineNum}: Nested tab group detected (Azure MCP CLI tab opened inside existing tab group).");
                cliTabCount++;
                inTabGroup = true;
                cliTabSeenInCurrentGroup = true;
                mcpTabSeenInCurrentGroup = false;
            }
            else if (line == "#### [MCP Server](#tab/mcp-server)")
            {
                if (!inTabGroup || !cliTabSeenInCurrentGroup)
                    errors.Add($"Line {lineNum}: MCP Server tab opened without preceding Azure MCP CLI tab.");
                else if (mcpTabSeenInCurrentGroup)
                    errors.Add($"Line {lineNum}: Nested tab group detected (MCP Server tab opened twice in the same tab group).");

                mcpTabCount++;
                mcpTabSeenInCurrentGroup = true;
            }
            else if (line == "---" && inTabGroup)
            {
                tabTerminatorCount++;
                inTabGroup = false;
                cliTabSeenInCurrentGroup = false;
                mcpTabSeenInCurrentGroup = false;
            }
        }

        if (mcpTabCount != cliTabCount)
            errors.Add($"Mismatched tab counts: {mcpTabCount} MCP Server tabs vs {cliTabCount} CLI tabs.");

        if (mcpTabCount != tabTerminatorCount)
            errors.Add($"Mismatched tab terminators: {mcpTabCount} tab groups opened but {tabTerminatorCount} terminated with ---.");

        if (inTabGroup)
            errors.Add("Unterminated tab group at end of document.");

        if (cliTabCount == 0)
            warnings.Add("No CLI tabs found in article.");

        return new ValidationResult(errors.Count == 0, errors, warnings);
    }
}
