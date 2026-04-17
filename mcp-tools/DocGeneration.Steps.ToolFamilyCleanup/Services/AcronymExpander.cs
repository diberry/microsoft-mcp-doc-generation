// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Expands acronyms on first body occurrence per Microsoft style guide
/// (Acrolinx TM-3: "Did you define the acronym in your content?").
///
/// Generalizes the existing PostProcessor.ExpandMcpAcronym() pattern
/// to handle VM, VMSS, RBAC, AKS, IaC, and other common Azure acronyms.
///
/// Rules:
/// - Only expands the FIRST body occurrence of each acronym
/// - Skips frontmatter and headings (H1, H2, etc.)
/// - Skips content inside backticks
/// - Idempotent — already-expanded text passes through unchanged
///
/// Fixes: #142 (generalized), #215
/// </summary>
public static class AcronymExpander
{
    private static readonly List<AcronymDefinition> Definitions = LoadDefinitions();

    // Matches fenced code blocks (```...```)
    private static readonly Regex CodeBlockPattern = new(
        @"```[\s\S]*?```",
        RegexOptions.Compiled);

    // Matches inline code spans (`...`)
    private static readonly Regex InlineCodePattern = new(
        @"`[^`]+`",
        RegexOptions.Compiled);

    // Matches heading lines (# ... or ## ... etc.)
    private static readonly Regex HeadingPattern = new(
        @"^#{1,6}\s.*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    /// <summary>
    /// Expands all known acronyms on their first body occurrence.
    /// </summary>
    public static string ExpandAll(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return "";

        // Protect code blocks, inline code, and headings with placeholders
        var placeholders = new Dictionary<string, string>();
        int placeholderIndex = 0;

        // Protect frontmatter
        string frontmatter = "";
        string body = markdown;
        if (markdown.StartsWith("---"))
        {
            int endFm = markdown.IndexOf("\n---", 3, StringComparison.Ordinal);
            if (endFm > 0)
            {
                int fmEnd = markdown.IndexOf('\n', endFm + 4);
                if (fmEnd > 0)
                {
                    frontmatter = markdown[..(fmEnd + 1)];
                    body = markdown[(fmEnd + 1)..];
                }
            }
        }

        // Protect code blocks
        body = CodeBlockPattern.Replace(body, m =>
        {
            var key = $"\x00CB{placeholderIndex++}\x00";
            placeholders[key] = m.Value;
            return key;
        });

        // Protect inline code
        body = InlineCodePattern.Replace(body, m =>
        {
            var key = $"\x00IC{placeholderIndex++}\x00";
            placeholders[key] = m.Value;
            return key;
        });

        // Protect headings
        body = HeadingPattern.Replace(body, m =>
        {
            var key = $"\x00HD{placeholderIndex++}\x00";
            placeholders[key] = m.Value;
            return key;
        });

        // Apply acronym expansion
        foreach (var def in Definitions)
        {
            body = ExpandFirstOccurrence(body, def);
        }

        // Restore placeholders (reverse order: headings, inline code, code blocks)
        foreach (var (key, value) in placeholders)
        {
            body = body.Replace(key, value);
        }

        return frontmatter + body;
    }

    /// <summary>
    /// Expands the first body occurrence of the given acronym.
    /// </summary>
    private static string ExpandFirstOccurrence(string text, AcronymDefinition def)
    {
        // If already expanded, skip
        if (text.Contains($"{def.Expansion} ({def.Acronym})", StringComparison.OrdinalIgnoreCase))
            return text;

        // Handle special context patterns (e.g., "Azure MCP Server")
        if (!string.IsNullOrEmpty(def.ContextPattern) && !string.IsNullOrEmpty(def.ExpandedForm))
        {
            int ctxIdx = text.IndexOf(def.ContextPattern, StringComparison.Ordinal);
            if (ctxIdx >= 0)
            {
                return string.Concat(
                    text.AsSpan(0, ctxIdx),
                    def.ExpandedForm,
                    text.AsSpan(ctxIdx + def.ContextPattern.Length));
            }
            return text;
        }

        // Standard pattern: find standalone acronym and expand
        var pattern = $@"\b{Regex.Escape(def.Acronym)}\b";
        var regex = new Regex(pattern, RegexOptions.None);
        var match = regex.Match(text);
        if (!match.Success)
            return text;

        // Replace only the first match
        var expanded = $"{def.Expansion} ({def.Acronym})";
        return string.Concat(
            text.AsSpan(0, match.Index),
            expanded,
            text.AsSpan(match.Index + match.Length));
    }

    /// <summary>
    /// Loads acronym definitions from the embedded config file.
    /// Falls back to built-in definitions if the file isn't found.
    /// </summary>
    private static List<AcronymDefinition> LoadDefinitions()
    {
        // Try to load from data directory
        var paths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "acronym-definitions.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "mcp-tools", "data", "acronym-definitions.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "data", "acronym-definitions.json"),
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                try
                {
                    var json = File.ReadAllText(path);
                    var defs = JsonSerializer.Deserialize<List<AcronymDefinition>>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (defs != null && defs.Count > 0)
                        return defs;
                }
                catch
                {
                    // Fall through to built-in defaults
                }
            }
        }

        // Built-in fallback definitions
        return
        [
            new() { Acronym = "MCP", Expansion = "Model Context Protocol",
                     ContextPattern = "Azure MCP Server",
                     ExpandedForm = "Azure Model Context Protocol (MCP) Server" },
            new() { Acronym = "VM", Expansion = "virtual machine" },
            new() { Acronym = "VMSS", Expansion = "virtual machine scale set" },
            new() { Acronym = "AKS", Expansion = "Azure Kubernetes Service" },
            new() { Acronym = "RBAC", Expansion = "role-based access control" },
            new() { Acronym = "IaC", Expansion = "infrastructure as code" },
        ];
    }
}

/// <summary>
/// Defines an acronym and its expansion for first-use annotation.
/// </summary>
public class AcronymDefinition
{
    public string Acronym { get; set; } = "";
    public string Expansion { get; set; } = "";

    /// <summary>
    /// Optional: a specific phrase pattern containing the acronym
    /// (e.g., "Azure MCP Server") that should be expanded as a whole.
    /// </summary>
    public string? ContextPattern { get; set; }

    /// <summary>
    /// Optional: the fully expanded form when ContextPattern matches
    /// (e.g., "Azure Model Context Protocol (MCP) Server").
    /// </summary>
    public string? ExpandedForm { get; set; }
}
