// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.RegularExpressions;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Shared helpers for tool-family regression test classes.
/// Centralizes file loading, repo discovery, and section parsing.
/// </summary>
internal static class RegressionTestHelpers
{
    private static readonly Lazy<string> _repoRoot = new(FindRepoRoot);
    private static readonly Lazy<string> _fixturesDir = new(FindFixturesDir);

    public static string RepoRoot => _repoRoot.Value;
    public static string FixturesDir => _fixturesDir.Value;

    public static string LoadFixture(string filename)
    {
        var path = Path.Combine(FixturesDir, filename);
        Assert.True(File.Exists(path), $"Fixture not found: {path}");
        return File.ReadAllText(path);
    }

    public static string LoadRealGeneratedFile(params string[] pathParts)
    {
        var path = Path.Combine(new[] { RepoRoot }.Concat(pathParts).ToArray());
        Assert.True(File.Exists(path), $"Generated file not found: {path}. Run generation first.");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Splits content into tool sections (one per H2), excluding "Related content".
    /// </summary>
    public static List<string> GetToolSections(string content)
    {
        var sections = new List<string>();
        var h2Matches = Regex.Matches(content, @"^## .+$", RegexOptions.Multiline);

        for (int i = 0; i < h2Matches.Count; i++)
        {
            var heading = h2Matches[i].Groups[0].Value;
            if (heading.Contains("Related content")) continue;

            var start = h2Matches[i].Index;
            var end = (i + 1 < h2Matches.Count) ? h2Matches[i + 1].Index : content.Length;
            sections.Add(content[start..end]);
        }
        return sections;
    }

    /// <summary>
    /// Counts standalone --- lines outside of code fences.
    /// </summary>
    public static int CountSeparatorsOutsideCodeBlocks(string text)
    {
        var lines = text.Split('\n');
        bool inCodeBlock = false;
        int count = 0;
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("```")) inCodeBlock = !inCodeBlock;
            if (!inCodeBlock && trimmed == "---") count++;
        }
        return count;
    }

    /// <summary>
    /// Extracts YAML frontmatter content (between first and second --- delimiters).
    /// </summary>
    public static string ExtractFrontmatter(string content)
    {
        var lines = content.Split('\n');
        if (lines[0].TrimEnd('\r') != "---") return "";

        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd('\r') == "---")
                return string.Join('\n', lines[1..i]);
        }
        return "";
    }

    /// <summary>
    /// Finds the character index of the tab separator (---) after the CLI tab content.
    /// </summary>
    public static int FindTabSeparatorIndex(string section)
    {
        var lines = section.Split('\n');
        bool inCodeBlock = false;
        int charIndex = 0;
        bool passedCliTab = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("```")) inCodeBlock = !inCodeBlock;
            if (trimmed.Contains("#### [Azure MCP CLI](#tab/azure-mcp-cli)")) passedCliTab = true;
            if (!inCodeBlock && trimmed == "---" && passedCliTab)
                return charIndex;
            charIndex += line.Length + 1; // +1 for \n
        }
        return -1;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
            if (File.Exists(Path.Combine(dir, "mcp-doc-generation.sln")))
                return dir;
        }
        throw new InvalidOperationException("Could not find repo root (mcp-doc-generation.sln)");
    }

    private static string FindFixturesDir()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "Fixtures");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }
        return Path.Combine(FindRepoRoot(), "mcp-tools", "DocGeneration.Steps.ToolFamilyCleanup.Tests", "Fixtures");
    }
}
