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
        // Double-decode to handle double-encoded entities (e.g., &amp;quot; → &quot; → ")
        var decodedDescription = DecodeHtmlEntities(DecodeHtmlEntities(rawDescription));

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
        var activation = ExtractActivationTriggers(decodedDescription, body);

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
            RawBody = body,
            Activation = activation
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
        // Preserve known acronyms, downcase other ALL-CAPS words that are internal markers
        var keepUpperInItems = new HashSet<string> {
            "API", "CLI", "SDK", "MCP", "RBAC", "ARM", "AKS", "SQL", "SMB",
            "AWS", "GCP", "KQL", "ADX", "MSAL", "OAuth", "IoT", "SaaS", "PaaS", "IaaS",
            "VM", "VMs", "VMSS", "HTTP", "HTTPS", "REST", "JSON", "YAML", "DNS", "CDN",
            "SSH", "SSL", "TLS", "SKU", "SLA", "URL", "URI"
        };
        clean = Regex.Replace(clean, @"\b[A-Z]{2,}\b", m =>
            keepUpperInItems.Contains(m.Value) ? m.Value
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
        var tools = new List<McpToolEntry>();

        // 1. Extract from dedicated MCP Tools / MCP Server sections
        var section = ExtractSectionContent(body, @"##?\s*(?:MCP\s+)?(?:Tools|Server)(?:\s*\([^)]*\))?");
        if (!string.IsNullOrEmpty(section))
        {
            // Parse tables with dynamic column detection
            tools.AddRange(ParseToolTable(section));

            // Parse bullet format: - `tool` with command `cmd` - description
            tools.AddRange(ParseToolBullets(section));

            // Parse code block tool references within the section
            tools.AddRange(ParseCodeBlockTools(section));
        }

        // 2. Extract from Quick Reference key-value table
        var quickRefSection = ExtractSectionContent(body, @"##?\s*Quick\s+Reference");
        if (!string.IsNullOrEmpty(quickRefSection))
        {
            tools.AddRange(ParseQuickReferenceTools(quickRefSection));
        }

        // Deduplicate: prefer richer entries (longer Purpose) for same ToolName+Command
        return DeduplicateTools(tools);
    }

    private static List<McpToolEntry> ParseToolTable(string section)
    {
        var tools = new List<McpToolEntry>();

        // Find all table rows (any column count)
        var rows = Regex.Matches(section, @"^\|(.+)\|?\s*$", RegexOptions.Multiline);
        if (rows.Count < 2) return tools;

        // Parse header row to detect column semantics
        var headerCells = SplitTableRow(rows[0].Groups[1].Value);
        if (headerCells.Count == 0) return tools;

        // Map column indices by normalized header name
        int toolCol = -1, commandCol = -1, purposeCol = -1, toolPageCol = -1;
        for (int i = 0; i < headerCells.Count; i++)
        {
            var h = headerCells[i].Trim().ToLowerInvariant();
            if (h is "tool" or "toolname" or "name")
                toolCol = i;
            else if (h is "command" or "cmd")
                commandCol = i;
            else if (h is "purpose" or "description" or "use when")
                purposeCol = i;
            else if (h is "toolpage" or "tool page")
                toolPageCol = i;
        }

        if (toolCol < 0) return tools;

        // Parse data rows (skip header and separator)
        for (int r = 1; r < rows.Count; r++)
        {
            var cells = SplitTableRow(rows[r].Groups[1].Value);
            if (cells.Count <= toolCol) continue;

            var rawTool = StripBackticks(cells[toolCol].Trim());
            if (string.IsNullOrWhiteSpace(rawTool) || rawTool.StartsWith("---")) continue;
            // Skip header-like values
            if (rawTool.Equals("Tool", StringComparison.OrdinalIgnoreCase) ||
                rawTool.Equals("ToolName", StringComparison.OrdinalIgnoreCase) ||
                rawTool.Equals("Name", StringComparison.OrdinalIgnoreCase)) continue;

            var command = commandCol >= 0 && cells.Count > commandCol
                ? StripBackticks(cells[commandCol].Trim()) : "";
            var purpose = purposeCol >= 0 && cells.Count > purposeCol
                ? StripBackticks(cells[purposeCol].Trim()) : "";
            var toolPage = toolPageCol >= 0 && cells.Count > toolPageCol
                ? NullIfEmpty(StripBackticks(cells[toolPageCol].Trim())) : null;

            tools.Add(new McpToolEntry(rawTool, command, purpose, toolPage));
        }

        return tools;
    }

    private static List<McpToolEntry> ParseToolBullets(string section)
    {
        var tools = new List<McpToolEntry>();

        // Format: - `tool` with command `cmd` - description
        var withCommand = Regex.Matches(section,
            @"^[-*]\s+`([^`]+)`\s+with\s+command\s+`([^`]+)`\s*[-—–]\s*(.+)$",
            RegexOptions.Multiline);
        foreach (Match m in withCommand)
        {
            tools.Add(new McpToolEntry(
                m.Groups[1].Value.Trim(),
                m.Groups[2].Value.Trim(),
                m.Groups[3].Value.Trim()));
        }

        // Format: - `command` — purpose (simple bullet, only if no "with command" matches)
        if (tools.Count == 0)
        {
            var bullets = Regex.Matches(section,
                @"^[-*]\s+`([^`]+)`\s*[—–-]\s*(.+)$",
                RegexOptions.Multiline);
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

    private static List<McpToolEntry> ParseCodeBlockTools(string section)
    {
        var tools = new List<McpToolEntry>();

        // Match mcp_azure_mcp_* or azure__* at the start of a code block line
        var codeBlocks = Regex.Matches(section, @"```[^\n]*\n(.*?)```", RegexOptions.Singleline);
        foreach (Match block in codeBlocks)
        {
            var blockContent = block.Groups[1].Value;
            var toolMatch = Regex.Match(blockContent, @"^\s*(mcp_azure_mcp_\w+|azure__\w+)", RegexOptions.Multiline);
            if (!toolMatch.Success) continue;

            var toolName = toolMatch.Groups[1].Value.Trim();
            var commandMatch = Regex.Match(blockContent, @"command:\s*""([^""]+)""", RegexOptions.IgnoreCase);
            var command = commandMatch.Success ? commandMatch.Groups[1].Value.Trim() : "";

            tools.Add(new McpToolEntry(toolName, command, ""));
        }

        return tools;
    }

    private static List<McpToolEntry> ParseQuickReferenceTools(string section)
    {
        var tools = new List<McpToolEntry>();

        // Quick Reference tables are key-value: | Property | Value |
        // Look for rows where property is "MCP Tools"
        var rows = Regex.Matches(section, @"^\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|?\s*$", RegexOptions.Multiline);
        foreach (Match row in rows)
        {
            var property = row.Groups[1].Value.Trim();
            if (!property.Contains("MCP", StringComparison.OrdinalIgnoreCase) ||
                !property.Contains("Tool", StringComparison.OrdinalIgnoreCase)) continue;

            var value = row.Groups[2].Value.Trim();
            // Extract backtick-wrapped tool names
            var toolRefs = Regex.Matches(value, @"`([^`]+)`");
            foreach (Match t in toolRefs)
            {
                var toolName = t.Groups[1].Value.Trim();
                tools.Add(new McpToolEntry(toolName, "", ""));
            }
        }

        return tools;
    }

    private static List<McpToolEntry> DeduplicateTools(List<McpToolEntry> tools)
    {
        if (tools.Count == 0) return tools;

        // Group by ToolName+Command (or just ToolName if Command matches)
        var result = new List<McpToolEntry>();
        var seen = new Dictionary<string, McpToolEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var tool in tools)
        {
            var key = $"{tool.ToolName}|{tool.Command}";
            if (seen.TryGetValue(key, out var existing))
            {
                // Prefer the entry with more information
                if (tool.Purpose.Length > existing.Purpose.Length)
                    seen[key] = tool;
            }
            else
            {
                // Also check by ToolName alone for entries with empty command
                var nameKey = $"{tool.ToolName}|";
                if (tool.Command == "" && seen.ContainsKey(nameKey))
                {
                    // Skip: we already have an entry for this tool name with no command
                    if (tool.Purpose.Length > seen[nameKey].Purpose.Length)
                        seen[nameKey] = tool;
                }
                else if (tool.Command == "")
                {
                    // Check if any existing entry has this tool name with a command
                    var existingWithCommand = seen.Values
                        .FirstOrDefault(t => t.ToolName.Equals(tool.ToolName, StringComparison.OrdinalIgnoreCase));
                    if (existingWithCommand != null)
                        continue; // Already have a richer entry
                    seen[nameKey] = tool;
                }
                else
                {
                    seen[key] = tool;
                }
            }
        }

        return seen.Values.ToList();
    }

    private static List<string> SplitTableRow(string rowContent)
    {
        // Split on | but handle edge cases (leading/trailing pipes already stripped by caller)
        return rowContent.Split('|')
            .Select(c => c.Trim())
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();
    }

    private static string StripBackticks(string value)
    {
        if (value.StartsWith('`') && value.EndsWith('`'))
            return value[1..^1];
        return value;
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

    /// <summary>
    /// Extracts RBAC role requirements from structured sections and inline mentions.
    /// Looks for "## Required Roles", "### RBAC", role tables, and inline role names.
    /// </summary>
    internal static List<RbacRequirement> ExtractRbacRoles(string body)
    {
        var roles = new List<RbacRequirement>();
        if (string.IsNullOrWhiteSpace(body)) return roles;

        // 1. Parse from structured sections: "## Required Roles", "### RBAC", "### Required roles"
        var sectionPatterns = new[]
        {
            @"#{2,3}\s*Required\s+Roles",
            @"#{2,3}\s*RBAC(?:\s+Roles)?",
            @"#{2,3}\s*Role[\s-]+Based\s+Access"
        };

        foreach (var pattern in sectionPatterns)
        {
            var section = ExtractRbacSectionContent(body, pattern);
            if (string.IsNullOrEmpty(section)) continue;

            // Parse table rows: | RoleName | Scope | Reason |
            var tableRoles = ParseRbacTable(section);
            if (tableRoles.Count > 0)
            {
                roles.AddRange(tableRoles);
                break;
            }

            // Parse bullet format: - **Role Name** — scope description
            var bulletRoles = ParseRbacBullets(section);
            if (bulletRoles.Count > 0)
            {
                roles.AddRange(bulletRoles);
                break;
            }
        }

        // 2. Parse inline role mentions from body text (e.g., "Cost Management Reader + Monitoring Reader")
        if (roles.Count == 0)
        {
            roles.AddRange(ExtractInlineRbacRoles(body));
        }

        // Deduplicate by role name (case-insensitive)
        return roles
            .GroupBy(r => r.RoleName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
    }

    private static List<RbacRequirement> ParseRbacTable(string section)
    {
        var roles = new List<RbacRequirement>();
        var rows = Regex.Matches(section, @"^\|\s*([^|]+?)\s*\|\s*([^|]+?)\s*\|(?:\s*([^|]*?)\s*\|)?", RegexOptions.Multiline);
        foreach (Match row in rows)
        {
            var roleName = row.Groups[1].Value.Trim();
            if (string.IsNullOrWhiteSpace(roleName) || roleName.StartsWith("---") ||
                roleName.Equals("Role", StringComparison.OrdinalIgnoreCase) ||
                roleName.Equals("RoleName", StringComparison.OrdinalIgnoreCase)) continue;

            var scope = row.Groups[2].Value.Trim();
            if (scope.StartsWith("---")) scope = "Subscription";
            var reason = row.Groups.Count > 3 ? NullIfEmpty(row.Groups[3].Value.Trim()) : null;
            if (reason != null && reason.StartsWith("---")) reason = null;
            roles.Add(new RbacRequirement(roleName, scope, reason));
        }
        return roles;
    }

    private static List<RbacRequirement> ParseRbacBullets(string section)
    {
        var roles = new List<RbacRequirement>();
        var bullets = Regex.Matches(section, @"^[-*]\s+\*?\*?([^*\n]+?)\*?\*?\s*[—–-]\s*(.+)$", RegexOptions.Multiline);
        foreach (Match b in bullets)
        {
            var roleName = b.Groups[1].Value.Trim();
            var description = b.Groups[2].Value.Trim();
            roles.Add(new RbacRequirement(roleName, "Subscription", description));
        }

        // Also try simple bullets: - Role Name
        if (roles.Count == 0)
        {
            var simples = Regex.Matches(section, @"^[-*]\s+(.+)$", RegexOptions.Multiline);
            foreach (Match s in simples)
            {
                var roleName = s.Groups[1].Value.Trim().Trim('*');
                if (IsLikelyRoleName(roleName))
                    roles.Add(new RbacRequirement(roleName, "Subscription"));
            }
        }
        return roles;
    }

    /// <summary>
    /// Extracts inline RBAC role mentions like "Cost Management Reader + Monitoring Reader"
    /// or "requires Contributor role" from body text.
    /// </summary>
    private static List<RbacRequirement> ExtractInlineRbacRoles(string body)
    {
        var roles = new List<RbacRequirement>();
        var knownSuffixes = new[] { "Reader", "Contributor", "Owner", "Administrator", "Operator" };

        // Match patterns like "Role Name Reader", "Role Name Contributor", etc.
        foreach (var suffix in knownSuffixes)
        {
            // Match multi-word names where each word starts uppercase, ending in known suffix
            var pattern = $@"(?:^|[\s,+&])((?:[A-Z][a-z]+ ){{1,6}}{Regex.Escape(suffix)})\b";
            var matches = Regex.Matches(body, pattern);
            foreach (Match m in matches)
            {
                var roleName = m.Groups[1].Value.Trim();
                // Filter out false positives: headers, markdown artifacts, table borders
                if (roleName.Length > 60 || roleName.Contains('#') || roleName.Contains('|')) continue;
                if (!IsLikelyRoleName(roleName)) continue;
                if (!roles.Any(r => r.RoleName.Equals(roleName, StringComparison.OrdinalIgnoreCase)))
                    roles.Add(new RbacRequirement(roleName, "Subscription"));
            }
        }

        return roles;
    }

    /// <summary>
    /// Checks if a string looks like an Azure RBAC role name (ends with Reader/Contributor/Owner/etc.).
    /// </summary>
    private static bool IsLikelyRoleName(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 4) return false;
        var roleSuffixes = new[] { "Reader", "Contributor", "Owner", "Administrator", "Operator" };
        return roleSuffixes.Any(s => text.EndsWith(s, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Extracts MANDATORY/PREFER OVER directives and codebase detection markers from description.
    /// </summary>
    internal static ActivationTrigger? ExtractActivationTriggers(string description, string body)
    {
        if (string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(body))
            return null;

        var fullText = $"{description}\n{body}";
        string? directive = null;
        string? preferOver = null;
        var markers = new List<string>();

        // Parse MANDATORY directive
        var mandatoryMatch = Regex.Match(fullText, @"\bMANDATORY\b\s*(?:when\s+)?(.+?)(?:\.|—|$)", RegexOptions.IgnoreCase);
        if (mandatoryMatch.Success)
            directive = $"MANDATORY {mandatoryMatch.Groups[1].Value.Trim()}";

        // Parse PREFER OVER directive
        var preferMatch = Regex.Match(fullText, @"\bPREFER\s+OVER\s+(\S+)", RegexOptions.IgnoreCase);
        if (preferMatch.Success)
            preferOver = preferMatch.Groups[1].Value.Trim().TrimEnd('.', ',', ';');

        // Set directive from PREFER OVER if MANDATORY not found
        if (directive == null && preferOver != null)
            directive = $"PREFER OVER {preferOver}";

        // Extract detection markers: patterns like @package/name, file patterns, class names in backticks
        var markerPatterns = new[]
        {
            @"@[\w-]+/[\w-]+",                           // npm packages: @github/copilot-sdk
            @"(?<=\bcodebase\s+contains\s+)`([^`]+)`",   // codebase contains `X`
            @"(?<=\b[Dd]etects?\s+)`([^`]+)`",           // detects `X` or Detects `X`
            @"(?<=\bmarkers?\s*:\s*)`([^`]+)`",           // markers: `X`
            @"(?<=and\s+)`([^`]+)`"                       // and `X` (continuation of detection list)
        };

        foreach (var pattern in markerPatterns)
        {
            var markerMatches = Regex.Matches(fullText, pattern);
            foreach (Match m in markerMatches)
            {
                var value = m.Groups.Count > 1 && m.Groups[1].Success
                    ? m.Groups[1].Value
                    : m.Value;
                if (!markers.Contains(value))
                    markers.Add(value);
            }
        }

        if (directive == null && markers.Count == 0)
            return null;

        return new ActivationTrigger(
            directive ?? "Auto-activates based on codebase detection",
            preferOver,
            markers.Count > 0 ? markers : null);
    }

    private static string ExtractSectionContent(string body, string headingPattern)
    {
        var match = Regex.Match(body, $@"^{headingPattern}\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (!match.Success) return "";

        var afterHeading = body[(match.Index + match.Length)..];
        var nextHeading = Regex.Match(afterHeading, @"^##?\s", RegexOptions.Multiline);
        return nextHeading.Success ? afterHeading[..nextHeading.Index].Trim() : afterHeading.Trim();
    }

    /// <summary>
    /// Extracts section content for RBAC headings, supporting ## and ### level headings.
    /// Uses string concatenation instead of interpolation to avoid issues with quantifier braces.
    /// </summary>
    private static string ExtractRbacSectionContent(string body, string headingPattern)
    {
        var fullPattern = "^" + headingPattern + @"\s*$";
        var match = Regex.Match(body, fullPattern, RegexOptions.Multiline | RegexOptions.IgnoreCase);
        if (!match.Success) return "";

        var afterHeading = body[(match.Index + match.Length)..];
        // Match next heading at any level (##, ###, etc.)
        var nextHeading = Regex.Match(afterHeading, @"^#{2,}", RegexOptions.Multiline);
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
            { "ai", "sdk", "rbac", "mcp", "api", "cli", "sql", "smb", "aks",
              "aws", "gcp", "kql", "adx", "msal", "iot", "vm", "http", "dns", "cdn" };

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

        // Strip everything after the EARLIEST of: USE FOR / WHEN / DO NOT USE markers
        var cutPatterns = new[] { @"\bUSE\s+FOR\b", @"\bWHEN\s*:", @"\bDO\s+NOT\s+USE", @"\bDON'?T\s+USE" };
        int earliestCut = clean.Length;
        foreach (var pattern in cutPatterns)
        {
            var match = Regex.Match(clean, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Index < earliestCut)
            {
                earliestCut = match.Index;
            }
        }
        if (earliestCut < clean.Length)
        {
            clean = clean[..earliestCut].TrimEnd(' ', '.', ',', ';');
        }

        // Downcase ALL-CAPS words (except known acronyms) for readability
        var keepUpper = new HashSet<string> {
            "AI", "API", "CLI", "SDK", "MCP", "RBAC", "AKS", "SQL", "SMB", "ARM",
            "AWS", "GCP", "KQL", "ADX", "MSAL", "OAuth", "IoT", "SaaS", "PaaS", "IaaS",
            "VM", "VMs", "VMSS", "HTTP", "HTTPS", "REST", "JSON", "YAML", "DNS", "CDN",
            "SSH", "SSL", "TLS", "TCP", "UDP", "IP", "URL", "URI", "SKU", "SLA",
            "NOT", "OR", "AND", "FOR", "USE"
        };
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
        clean = Regex.Replace(clean, @"(\w[\w\s]+?)\s*\(\1\s*\((\w+)\)\)", "$1 ($2)", RegexOptions.IgnoreCase);

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
