using System.Text.RegularExpressions;
using SkillsGen.Core.Models;

namespace SkillsGen.Core.Parsers;

public partial class SkillMarkdownParser : ISkillParser
{
    public SkillData Parse(string skillName, string markdownContent)
    {
        if (string.IsNullOrWhiteSpace(markdownContent))
        {
            return new SkillData
            {
                Name = skillName,
                DisplayName = skillName,
                Description = ""
            };
        }

        var (frontmatter, body) = SplitFrontmatter(markdownContent);
        var name = ExtractFrontmatterField(frontmatter, "name") ?? skillName;
        var rawDisplayName = ExtractFrontmatterField(frontmatter, "display_?name");
        var displayName = rawDisplayName ?? DeriveDisplayName(name);
        var rawDescription = ExtractFrontmatterField(frontmatter, "description") ?? "";

        // Decode HTML entities FIRST so all extraction works on clean text
        var decodedDescription = DecodeHtmlEntities(rawDescription);

        // Clean the description for display (strip markers, take first 2 sentences)
        var cleanDescription = CleanDescription(decodedDescription);

        var useFor = ExtractListFromDescription(decodedDescription, @"USE\s+FOR:");
        var whenItems = ExtractListFromDescription(decodedDescription, @"WHEN\s*:");
        var doNotUseFor = ExtractListFromDescription(decodedDescription, @"DO\s+NOT\s+USE\s+FOR:");
        var dontUseWhen = ExtractListFromDescription(decodedDescription, @"DON'?T\s+USE\s+WHEN\s*:");

        // Merge WHEN items into UseFor — clean up quoted trigger phrases
        foreach (var item in whenItems)
        {
            var cleaned = item.Trim().Trim('"', '\'', ',', '.');
            if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 3 && !useFor.Contains(cleaned))
                useFor.Add(cleaned);
        }

        // Merge DON'T USE WHEN into DoNotUseFor
        foreach (var item in dontUseWhen)
        {
            var cleaned = item.Trim().Trim('"', '\'', ',', '.');
            if (!string.IsNullOrWhiteSpace(cleaned) && cleaned.Length > 3 && !doNotUseFor.Contains(cleaned))
                doNotUseFor.Add(cleaned);
        }

        // Also extract from body sections
        if (useFor.Count == 0)
            useFor = ExtractBulletSection(body, @"##?\s*(?:When to )?[Uu]se\s*(?:for|this)?");
        if (doNotUseFor.Count == 0)
            doNotUseFor = ExtractBulletSection(body, @"##?\s*(?:When )?(?:[Dd]o\s*[Nn]ot|[Dd]on'?t)\s*[Uu]se\s*(?:for|this)?");

        var services = ExtractServices(body);
        var mcpTools = ExtractMcpTools(body);
        var workflowSteps = ExtractWorkflowSteps(body);
        var decisionGuidance = ExtractDecisionGuidance(body);
        var relatedSkills = ExtractRelatedSkills(body);
        var prerequisites = ExtractPrerequisites(body);

        return new SkillData
        {
            Name = name,
            DisplayName = displayName,
            Description = cleanDescription,
            UseFor = useFor,
            DoNotUseFor = doNotUseFor,
            Services = services,
            McpTools = mcpTools,
            WorkflowSteps = workflowSteps,
            DecisionGuidance = decisionGuidance,
            RelatedSkills = relatedSkills,
            Prerequisites = prerequisites,
            RawBody = body
        };
    }

    private static (string frontmatter, string body) SplitFrontmatter(string content)
    {
        var match = FrontmatterRegex().Match(content);
        if (!match.Success)
            return ("", content);

        var frontmatter = match.Groups[1].Value;
        var body = content[match.Length..].TrimStart();
        return (frontmatter, body);
    }

    private static string? ExtractFrontmatterField(string frontmatter, string fieldPattern)
    {
        if (string.IsNullOrEmpty(frontmatter)) return null;

        try
        {
            var regex = new Regex($@"^{fieldPattern}\s*:\s*(.+)$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            var match = regex.Match(frontmatter);
            if (!match.Success) return null;

            var value = match.Groups[1].Value.Trim();
            // Strip surrounding quotes
            if ((value.StartsWith('"') && value.EndsWith('"')) ||
                (value.StartsWith('\'') && value.EndsWith('\'')))
            {
                value = value[1..^1];
            }
            // Unescape YAML escaped quotes
            value = value.Replace("\\\"", "\"").Replace("\\'", "'");
            return value;
        }
        catch (RegexParseException)
        {
            return null;
        }
    }

    private static List<string> ExtractListFromDescription(string description, string marker)
    {
        try
        {
            var idx = description.IndexOf(marker.Replace(@"\s+", " ").Replace(@"\s*", ""), StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                // Try regex match
                var markerMatch = Regex.Match(description, marker, RegexOptions.IgnoreCase);
                if (!markerMatch.Success) return [];
                idx = markerMatch.Index;
                var afterMarker = description[(idx + markerMatch.Length)..].Trim();
                return ParseCommaSeparatedOrBullets(afterMarker);
            }

            var markerLen = Regex.Match(description[idx..], marker, RegexOptions.IgnoreCase);
            var after = description[(idx + markerLen.Length)..].Trim();
            return ParseCommaSeparatedOrBullets(after);
        }
        catch (RegexParseException)
        {
            return [];
        }
    }

    private static List<string> ParseCommaSeparatedOrBullets(string text)
    {
        try
        {
            return ParseCommaSeparatedOrBulletsCore(text);
        }
        catch (RegexParseException)
        {
            return [];
        }
    }

    private static List<string> ParseCommaSeparatedOrBulletsCore(string text)
    {
        // Stop at next marker or end of string
        var stopPatterns = new[] {
            @"DO\s+NOT\s+USE",
            @"DON'?T\s+USE",
            @"don[\u2019']?t\s+USE",
        };
        foreach (var stopPattern in stopPatterns)
        {
            var stopIdx = Regex.Match(text, stopPattern, RegexOptions.IgnoreCase);
            if (stopIdx.Success)
            {
                text = text[..stopIdx.Index];
                break;
            }
        }

        var items = new List<string>();
        // Try bullet points first
        var bulletMatches = Regex.Matches(text, @"[-*]\s+(.+?)(?=\n[-*]|\n\n|$)", RegexOptions.Singleline);
        if (bulletMatches.Count > 0)
        {
            foreach (Match m in bulletMatches)
                items.Add(CleanListItem(m.Groups[1].Value));
            return items.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
        }

        // Try quoted strings: "item1", "item2"
        var quotedMatches = Regex.Matches(text, @"""([^""]+)""");
        if (quotedMatches.Count > 0)
        {
            foreach (Match m in quotedMatches)
                items.Add(CleanListItem(m.Groups[1].Value));
            return items.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
        }

        // Fall back to comma-separated
        var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            var clean = CleanListItem(part);
            if (!string.IsNullOrWhiteSpace(clean))
                items.Add(clean);
        }
        return items;
    }

    private static string CleanListItem(string item)
    {
        var clean = item.Trim();
        // Remove HTML entities
        clean = DecodeHtmlEntities(clean);
        // Strip quotes, periods, trailing markers
        clean = clean.Trim('"', '\'', '.', ',', ';', ' ');
        // Remove any remaining control text fragments
        clean = Regex.Replace(clean, @"\b(USE\s+FOR|DO\s+NOT\s+USE|WHEN\s*:|DON'?T\s+USE)\b.*$", "", RegexOptions.IgnoreCase).Trim();
        // Remove ALL CAPS words that are internal markers
        clean = Regex.Replace(clean, @"\b[A-Z]{3,}\b", m =>
            m.Value is "API" or "CLI" or "SDK" or "MCP" or "RBAC" or "ARM" or "AKS" or "SQL" or "SMB"
                ? m.Value
                : m.Value[0] + m.Value[1..].ToLower()
        );
        return clean.Trim();
    }

    private static List<string> ExtractBulletSection(string body, string headingPattern)
    {
        var headingMatch = Regex.Match(body, $@"^{headingPattern}\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (!headingMatch.Success) return [];

        var afterHeading = body[(headingMatch.Index + headingMatch.Length)..];
        var nextHeading = Regex.Match(afterHeading, @"^##?\s", RegexOptions.Multiline);
        var sectionText = nextHeading.Success ? afterHeading[..nextHeading.Index] : afterHeading;

        return ExtractBulletItems(sectionText);
    }

    private static List<string> ExtractBulletItems(string text)
    {
        var items = new List<string>();
        var matches = Regex.Matches(text, @"^[-*]\s+(.+)$", RegexOptions.Multiline);
        foreach (Match m in matches)
        {
            var item = m.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(item))
                items.Add(item);
        }
        return items;
    }

    private static List<ServiceEntry> ExtractServices(string body)
    {
        var section = ExtractSectionContent(body, @"##?\s*(?:Azure\s+)?Services");
        if (string.IsNullOrEmpty(section)) return [];

        var services = new List<ServiceEntry>();
        // Parse table rows: | Name | UseWhen | McpTools | Cli |
        var rows = Regex.Matches(section, @"^\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]*?)\s*\|\s*([^|]*?)\s*\|?\s*$", RegexOptions.Multiline);
        foreach (Match row in rows)
        {
            var name = row.Groups[1].Value.Trim();
            if (name.StartsWith("---") || name.Equals("Name", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Service", StringComparison.OrdinalIgnoreCase)) continue;

            var useWhen = row.Groups[2].Value.Trim();
            var mcpTools = NullIfEmpty(row.Groups[3].Value.Trim());
            var cli = NullIfEmpty(row.Groups[4].Value.Trim());
            services.Add(new ServiceEntry(name, useWhen, mcpTools, cli));
        }

        // Also try bullet-point format: - **Name**: description
        if (services.Count == 0)
        {
            var bullets = Regex.Matches(section, @"^[-*]\s+\*\*(.+?)\*\*:?\s*(.+)$", RegexOptions.Multiline);
            foreach (Match b in bullets)
            {
                services.Add(new ServiceEntry(b.Groups[1].Value.Trim(), b.Groups[2].Value.Trim()));
            }
        }

        return services;
    }

    private static List<McpToolEntry> ExtractMcpTools(string body)
    {
        var section = ExtractSectionContent(body, @"##?\s*(?:MCP\s+)?Tools");
        if (string.IsNullOrEmpty(section)) return [];

        var tools = new List<McpToolEntry>();
        // Parse table: | ToolName | Command | Purpose | ToolPage |
        var rows = Regex.Matches(section, @"^\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]*?)\s*\|?\s*$", RegexOptions.Multiline);
        foreach (Match row in rows)
        {
            var toolName = row.Groups[1].Value.Trim();
            if (toolName.StartsWith("---") || toolName.Equals("ToolName", StringComparison.OrdinalIgnoreCase) ||
                toolName.Equals("Tool", StringComparison.OrdinalIgnoreCase) ||
                toolName.Equals("Name", StringComparison.OrdinalIgnoreCase)) continue;

            var command = row.Groups[2].Value.Trim();
            var purpose = row.Groups[3].Value.Trim();
            var toolPage = NullIfEmpty(row.Groups[4].Value.Trim());
            tools.Add(new McpToolEntry(toolName, command, purpose, toolPage));
        }

        // Bullet format: - `command` — purpose
        if (tools.Count == 0)
        {
            var bullets = Regex.Matches(section, @"^[-*]\s+`(.+?)`\s*[—–-]\s*(.+)$", RegexOptions.Multiline);
            foreach (Match b in bullets)
            {
                var cmd = b.Groups[1].Value.Trim();
                var purpose = b.Groups[2].Value.Trim();
                var toolNamePart = cmd.Split(' ')[0];
                tools.Add(new McpToolEntry(toolNamePart, cmd, purpose));
            }
        }

        return tools;
    }

    private static List<string> ExtractWorkflowSteps(string body)
    {
        var section = ExtractSectionContent(body, @"##?\s*(?:Steps|Workflow|Suggested\s+Workflow)");
        if (string.IsNullOrEmpty(section)) return [];

        var steps = new List<string>();
        // Numbered list: 1. Step description
        var numbered = Regex.Matches(section, @"^\d+\.\s+(.+)$", RegexOptions.Multiline);
        if (numbered.Count > 0)
        {
            foreach (Match m in numbered)
                steps.Add(m.Groups[1].Value.Trim());
            return steps;
        }

        // Bullet list
        return ExtractBulletItems(section);
    }

    private static List<DecisionEntry> ExtractDecisionGuidance(string body)
    {
        var section = ExtractSectionContent(body, @"##?\s*(?:Decision\s+Guidance|Decision|Guidance)");
        if (string.IsNullOrEmpty(section)) return [];

        var entries = new List<DecisionEntry>();
        // Look for sub-headings as topics
        var topicMatches = Regex.Matches(section, @"^###\s+(.+)$", RegexOptions.Multiline);
        if (topicMatches.Count > 0)
        {
            for (int i = 0; i < topicMatches.Count; i++)
            {
                var topic = topicMatches[i].Groups[1].Value.Trim();
                var start = topicMatches[i].Index + topicMatches[i].Length;
                var end = i + 1 < topicMatches.Count ? topicMatches[i + 1].Index : section.Length;
                var subsection = section[start..end];

                var options = ParseDecisionOptions(subsection);
                if (options.Count > 0)
                    entries.Add(new DecisionEntry(topic, options));
            }
        }
        else
        {
            // Try table format
            var options = ParseDecisionOptions(section);
            if (options.Count > 0)
                entries.Add(new DecisionEntry("General", options));
        }

        return entries;
    }

    private static List<DecisionOption> ParseDecisionOptions(string text)
    {
        var options = new List<DecisionOption>();

        // Table: | Option | BestFor | Tradeoff |
        var rows = Regex.Matches(text, @"^\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|\s*([^|]*?)\s*\|?\s*$", RegexOptions.Multiline);
        foreach (Match row in rows)
        {
            var option = row.Groups[1].Value.Trim();
            if (option.StartsWith("---") || option.Equals("Option", StringComparison.OrdinalIgnoreCase)) continue;
            var bestFor = row.Groups[2].Value.Trim();
            var tradeoff = row.Groups[3].Success ? NullIfEmpty(row.Groups[3].Value.Trim()) : null;
            options.Add(new DecisionOption(option, bestFor, tradeoff));
        }

        // Bullet: - **Option**: best for... (tradeoff: ...)
        if (options.Count == 0)
        {
            var bullets = Regex.Matches(text, @"^[-*]\s+\*\*(.+?)\*\*:?\s*(.+)$", RegexOptions.Multiline);
            foreach (Match b in bullets)
            {
                var option = b.Groups[1].Value.Trim();
                var rest = b.Groups[2].Value.Trim();
                var tradeoffMatch = Regex.Match(rest, @"\(tradeoff:?\s*(.+?)\)$", RegexOptions.IgnoreCase);
                string bestFor;
                string? tradeoff = null;
                if (tradeoffMatch.Success)
                {
                    bestFor = rest[..tradeoffMatch.Index].Trim().TrimEnd(',');
                    tradeoff = tradeoffMatch.Groups[1].Value.Trim();
                }
                else
                {
                    bestFor = rest;
                }
                options.Add(new DecisionOption(option, bestFor, tradeoff));
            }
        }

        return options;
    }

    private static List<string> ExtractRelatedSkills(string body)
    {
        var skills = new List<string>();
        // Match cross-references like @azure-storage or [azure-deploy](/skills/azure-deploy)
        var refs = Regex.Matches(body, @"@(azure-[\w-]+)");
        foreach (Match m in refs)
            skills.Add(m.Groups[1].Value);

        var links = Regex.Matches(body, @"\[([^\]]+)\]\([^)]*?/skills/(azure-[\w-]+)");
        foreach (Match m in links)
        {
            var name = m.Groups[2].Value;
            if (!skills.Contains(name))
                skills.Add(name);
        }

        // Also check "Related Skills" section
        var section = ExtractSectionContent(body, @"##?\s*Related\s+Skills");
        if (!string.IsNullOrEmpty(section))
        {
            var items = ExtractBulletItems(section);
            foreach (var item in items)
            {
                var nameMatch = Regex.Match(item, @"(azure-[\w-]+)");
                if (nameMatch.Success && !skills.Contains(nameMatch.Groups[1].Value))
                    skills.Add(nameMatch.Groups[1].Value);
            }
        }

        return skills;
    }

    private static List<string> ExtractPrerequisites(string body)
    {
        var section = ExtractSectionContent(body, @"##?\s*Prerequisites");
        if (string.IsNullOrEmpty(section)) return [];
        return ExtractBulletItems(section);
    }

    private static string ExtractSectionContent(string body, string headingPattern)
    {
        var match = Regex.Match(body, $@"^{headingPattern}\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (!match.Success) return "";

        var afterHeading = body[(match.Index + match.Length)..];
        var nextHeading = Regex.Match(afterHeading, @"^##?\s", RegexOptions.Multiline);
        return nextHeading.Success ? afterHeading[..nextHeading.Index].Trim() : afterHeading.Trim();
    }

    private static string DecodeHtmlEntities(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        return text.Replace("\\&quot;", "\"")
                   .Replace("&quot;", "\"")
                   .Replace("&#8212;", "—")
                   .Replace("&#8211;", "–")
                   .Replace("&amp;", "&")
                   .Replace("&lt;", "<")
                   .Replace("&gt;", ">")
                   .Replace("&#39;", "'")
                   .Replace("\\\"", "\"");
    }

    /// <summary>
    /// Converts a skill slug like "azure-deploy" to a display name like "Azure Deploy".
    /// Handles special cases: "ai" → "AI", "sdk" → "SDK", "rbac" → "RBAC", etc.
    /// </summary>
    private static string DeriveDisplayName(string slug)
    {
        var acronyms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "ai", "sdk", "rbac", "mcp", "api", "cli", "sql", "smb", "aks" };

        var words = slug.Split('-', StringSplitOptions.RemoveEmptyEntries);
        var result = words.Select(w =>
            acronyms.Contains(w) ? w.ToUpperInvariant()
            : char.ToUpper(w[0]) + w[1..]
        );
        return string.Join(" ", result);
    }

    private static string CleanDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description)) return "";

        var clean = description;

        // Strip everything after USE FOR: / WHEN: / DO NOT USE markers
        var cutPatterns = new[] { @"\bUSE\s+FOR\b", @"\bWHEN\s*:", @"\bDO\s+NOT\s+USE", @"\bDON'?T\s+USE" };
        foreach (var pattern in cutPatterns)
        {
            var match = Regex.Match(clean, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                clean = clean[..match.Index].TrimEnd(' ', '.', ',', ';');
                break;
            }
        }

        // Downcase ALL-CAPS words (except known acronyms) for readability
        var keepUpper = new HashSet<string> { "AI", "API", "CLI", "SDK", "MCP", "RBAC", "AKS", "SQL", "SMB", "ARM", "NOT", "OR", "AND" };
        clean = Regex.Replace(clean, @"\b[A-Z]{2,}(?:-[A-Z]+)*\b", m =>
            keepUpper.Contains(m.Value) ? m.Value : m.Value.ToLower());

        // Take only the first 2 sentences
        var sentences = Regex.Matches(clean, @"[^.!?]*[.!?]");
        if (sentences.Count > 2)
        {
            clean = string.Join(" ", sentences.Cast<Match>().Take(2).Select(m => m.Value.Trim()));
        }

        // Fix orphaned periods (". azure/" → ".azure/") — must run AFTER sentence join
        clean = clean.Replace(". azure/", ".azure/").Replace(". Azure/", ".azure/");

        // Remove duplicate acronym expansions: "ServiceName (ServiceName (ACRONYM))" → "ServiceName (ACRONYM)"
        clean = Regex.Replace(clean, @"(\w[\w\s]+?)\s*\(\1\s*\((\w+)\)\)", "$1 ($2)");

        clean = clean.Trim().TrimEnd('.');
        if (!string.IsNullOrEmpty(clean))
            clean += ".";

        return clean;
    }

    private static string? NullIfEmpty(string value)
    {
        return string.IsNullOrWhiteSpace(value) || value == "-" ? null : value;
    }

    [GeneratedRegex(@"^---\s*\n(.*?)\n---\s*\n", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();
}
