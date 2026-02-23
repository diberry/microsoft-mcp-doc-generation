using System.Text.RegularExpressions;
using AzmcpCommandParser.Models;

namespace AzmcpCommandParser.Parsing;

/// <summary>
/// Parses the azmcp-commands.md markdown file into a structured <see cref="CommandDocument"/>.
/// </summary>
public sealed partial class MarkdownCommandParser
{
    // ── Compiled regex patterns ──────────────────────────────────────────

    [GeneratedRegex(@"^#\s+(.+)$")]
    private static partial Regex H1Regex();

    [GeneratedRegex(@"^##\s+(.+)$")]
    private static partial Regex H2Regex();

    [GeneratedRegex(@"^###\s+(.+)$")]
    private static partial Regex H3Regex();

    [GeneratedRegex(@"^####\s+(.+)$")]
    private static partial Regex H4Regex();

    [GeneratedRegex(@"^```(\w*)$")]
    private static partial Regex CodeFenceRegex();

    [GeneratedRegex(@"^\|\s*`?-{2,}")]
    private static partial Regex TableSeparatorRegex();

    // Metadata line: # ❌ Destructive | ✅ Idempotent | ...
    [GeneratedRegex(@"^#\s*([❌✅])\s*Destructive\s*\|\s*([❌✅])\s*Idempotent\s*\|\s*([❌✅])\s*OpenWorld\s*\|\s*([❌✅])\s*ReadOnly\s*\|\s*([❌✅])\s*Secret\s*\|\s*([❌✅])\s*LocalRequired")]
    private static partial Regex MetadataRegex();

    // Command line: azmcp <words> --param <value> ...
    [GeneratedRegex(@"^azmcp\s+")]
    private static partial Regex CommandLineRegex();

    // Parameter: --name <value> or [--name <value>] or --flag
    [GeneratedRegex(@"(?<optional>\[)?--(?<name>[\w-]+)(?:\s*,\s*`?--(?<alias>[\w-]+)`?)?\s*(?:<(?<value>[^>]+)>)?(?:\])?")]
    private static partial Regex ParameterRegex();

    // Bracketed alternative group: [--param-a <value-a> | --param-b <value-b> ...]
    [GeneratedRegex(@"\[([^\[\]]*\|[^\[\]]*)\]")]
    private static partial Regex AlternativeGroupRegex();

    // Table row: | cell | cell | ... |
    [GeneratedRegex(@"^\|\s*(.+)\s*\|$")]
    private static partial Regex TableRowRegex();

    /// <summary>
    /// Parses markdown content into a <see cref="CommandDocument"/>.
    /// </summary>
    public CommandDocument Parse(string markdown)
    {
        var lines = markdown.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var doc = new CommandDocument();

        var i = 0;
        // Parse H1 title
        while (i < lines.Length)
        {
            var m = H1Regex().Match(lines[i]);
            if (m.Success)
            {
                doc.Title = m.Groups[1].Value.Trim();
                i++;
                break;
            }
            i++;
        }

        // Collect introduction text until first H2
        var introLines = new List<string>();
        while (i < lines.Length && !H2Regex().IsMatch(lines[i]))
        {
            introLines.Add(lines[i]);
            i++;
        }
        doc.Introduction = JoinTrimmed(introLines);

        // Process H2 sections
        while (i < lines.Length)
        {
            var h2 = H2Regex().Match(lines[i]);
            if (!h2.Success) { i++; continue; }

            var heading = h2.Groups[1].Value.Trim();
            i++;

            if (heading == "Global Options")
                (doc.GlobalOptions, i) = ParseGlobalOptionsSection(lines, i);
            else if (heading == "Available Commands")
                i = ParseAvailableCommands(lines, i, doc);
            else if (heading == "Response Format")
                (doc.ResponseFormat, i) = ParseResponseFormat(lines, i);
            else if (heading == "Error Handling")
                (doc.ErrorHandling, i) = ParseTextUntilH2(lines, i);
            else
                (_, i) = ParseTextUntilH2(lines, i); // skip unknown H2s
        }

        return doc;
    }

    /// <summary>
    /// Parses markdown from a file path.
    /// </summary>
    public CommandDocument ParseFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        return Parse(content);
    }

    // ── Section parsers ──────────────────────────────────────────────────

    private (List<GlobalOption>, int) ParseGlobalOptionsSection(string[] lines, int i)
    {
        var options = new List<GlobalOption>();

        // Find the table
        while (i < lines.Length && !H2Regex().IsMatch(lines[i]))
        {
            if (IsTableHeaderRow(lines, i))
            {
                var (entries, next) = ParseOptionTable(lines, i);
                foreach (var e in entries)
                {
                    options.Add(new GlobalOption
                    {
                        Name = CleanInlineCode(e.Name),
                        IsRequired = e.IsRequired,
                        Default = e.Default,
                        Description = e.Description
                    });
                }
                i = next;
            }
            else
            {
                i++;
            }
        }

        return (options, i);
    }

    private int ParseAvailableCommands(string[] lines, int i, CommandDocument doc)
    {
        while (i < lines.Length && !H2Regex().IsMatch(lines[i]))
        {
            var h3 = H3Regex().Match(lines[i]);
            if (h3.Success)
            {
                var heading = h3.Groups[1].Value.Trim();
                i++;

                if (heading == "Server Operations")
                {
                    (doc.ServerOperations, i) = ParseServerOperations(lines, i);
                }
                else if (heading.StartsWith("Azure") || heading.StartsWith("Bicep") || heading.StartsWith("Cloud") || heading.StartsWith("Microsoft"))
                {
                    var (section, next) = ParseServiceSection(lines, i, heading);
                    doc.ServiceSections.Add(section);
                    i = next;
                }
                else
                {
                    // Generic H3 section (e.g., tool listing metadata sections)
                    var (section, next) = ParseServiceSection(lines, i, heading);
                    doc.ServiceSections.Add(section);
                    i = next;
                }
            }
            else
            {
                i++;
            }
        }
        return i;
    }

    private (ServerOperations, int) ParseServerOperations(string[] lines, int i)
    {
        var ops = new ServerOperations();
        var contentLines = new List<string>();

        while (i < lines.Length && !H3Regex().IsMatch(lines[i]) && !H2Regex().IsMatch(lines[i]))
        {
            var h4 = H4Regex().Match(lines[i]);
            if (h4.Success)
            {
                var modeName = h4.Groups[1].Value.Trim();
                i++;

                if (modeName == "Server Start Command Options")
                {
                    // Parse the options table
                    while (i < lines.Length && !H4Regex().IsMatch(lines[i]) && !H3Regex().IsMatch(lines[i]) && !H2Regex().IsMatch(lines[i]))
                    {
                        if (IsTableHeaderRow(lines, i))
                        {
                            var (entries, next) = ParseOptionTable(lines, i);
                            ops.StartOptions.AddRange(entries.Select(e => new ServerStartOption
                            {
                                Name = CleanInlineCode(e.Name),
                                IsRequired = e.IsRequired,
                                Default = e.Default,
                                Description = e.Description
                            }));
                            i = next;
                        }
                        else
                        {
                            contentLines.Add(lines[i]);
                            i++;
                        }
                    }
                }
                else
                {
                    var mode = new ServerMode { Name = modeName };
                    var descLines = new List<string>();

                    while (i < lines.Length && !H4Regex().IsMatch(lines[i]) && !H3Regex().IsMatch(lines[i]) && !H2Regex().IsMatch(lines[i]))
                    {
                        if (lines[i].StartsWith("```"))
                        {
                            var (block, next) = ExtractCodeBlock(lines, i);
                            mode.CodeBlocks.Add(block);
                            i = next;
                        }
                        else
                        {
                            descLines.Add(lines[i]);
                            i++;
                        }
                    }
                    mode.Description = JoinTrimmed(descLines);
                    ops.Modes.Add(mode);
                }
            }
            else
            {
                contentLines.Add(lines[i]);
                i++;
            }
        }

        ops.Description = JoinTrimmed(contentLines);
        ops.RawContent = ops.Description;
        return (ops, i);
    }

    private (ServiceSection, int) ParseServiceSection(string[] lines, int i, string heading)
    {
        var section = new ServiceSection
        {
            Heading = heading,
            AreaName = DeriveAreaName(heading)
        };

        var descLines = new List<string>();

        while (i < lines.Length && !H3Regex().IsMatch(lines[i]) && !H2Regex().IsMatch(lines[i]))
        {
            var h4 = H4Regex().Match(lines[i]);
            if (h4.Success)
            {
                // Flush description
                if (descLines.Count > 0)
                {
                    section.Description = JoinTrimmed(descLines);
                    descLines.Clear();
                }

                var subHeading = h4.Groups[1].Value.Trim();
                i++;
                var (sub, next) = ParseSubSection(lines, i, subHeading);
                section.SubSections.Add(sub);
                i = next;
            }
            else if (lines[i].StartsWith("```"))
            {
                var (block, next) = ExtractCodeBlock(lines, i);
                var commands = ParseCommandsFromCodeBlock(block, i);
                section.Commands.AddRange(commands);
                i = next;
            }
            else if (IsTableHeaderRow(lines, i))
            {
                var (entries, next) = ParseParameterTableEntries(lines, i);
                section.ParameterTables.Add(new ParameterTable { Entries = entries });
                i = next;
            }
            else
            {
                descLines.Add(lines[i]);
                i++;
            }
        }

        if (descLines.Count > 0)
            section.Description = JoinTrimmed(descLines);

        return (section, i);
    }

    private (SubSection, int) ParseSubSection(string[] lines, int i, string heading)
    {
        var sub = new SubSection { Heading = heading };
        var descLines = new List<string>();

        while (i < lines.Length &&
               !H4Regex().IsMatch(lines[i]) &&
               !H3Regex().IsMatch(lines[i]) &&
               !H2Regex().IsMatch(lines[i]))
        {
            if (lines[i].StartsWith("```"))
            {
                var (block, next) = ExtractCodeBlock(lines, i);
                var commands = ParseCommandsFromCodeBlock(block, i);
                sub.Commands.AddRange(commands);
                i = next;
            }
            else if (IsTableHeaderRow(lines, i))
            {
                var (entries, next) = ParseParameterTableEntries(lines, i);
                sub.ParameterTables.Add(new ParameterTable { Entries = entries });
                i = next;
            }
            else
            {
                descLines.Add(lines[i]);
                i++;
            }
        }

        sub.Description = JoinTrimmed(descLines);
        return (sub, i);
    }

    private (ResponseFormat, int) ParseResponseFormat(string[] lines, int i)
    {
        var rf = new ResponseFormat();
        var descLines = new List<string>();

        while (i < lines.Length && !H2Regex().IsMatch(lines[i]))
        {
            if (lines[i].StartsWith("```"))
            {
                var (block, next) = ExtractCodeBlock(lines, i);
                rf.JsonSchema = block;
                i = next;
            }
            else if (IsTableHeaderRow(lines, i))
            {
                var (entries, next) = ParseParameterTableEntries(lines, i);
                rf.Fields.AddRange(entries.Select(e => new ResponseField
                {
                    Name = CleanInlineCode(e.Name),
                    Description = e.Description
                }));
                i = next;
            }
            else if (H3Regex().IsMatch(lines[i]))
            {
                i++; // skip sub-headings within Response Format
            }
            else
            {
                descLines.Add(lines[i]);
                i++;
            }
        }

        rf.Description = JoinTrimmed(descLines);
        return (rf, i);
    }

    private (string text, int next) ParseTextUntilH2(string[] lines, int i)
    {
        var textLines = new List<string>();
        while (i < lines.Length && !H2Regex().IsMatch(lines[i]))
        {
            textLines.Add(lines[i]);
            i++;
        }
        return (JoinTrimmed(textLines), i);
    }

    // ── Code block & command parsing ─────────────────────────────────────

    private (string block, int next) ExtractCodeBlock(string[] lines, int i)
    {
        var content = new List<string>();
        i++; // skip opening fence
        while (i < lines.Length && !lines[i].TrimStart().StartsWith("```"))
        {
            content.Add(lines[i]);
            i++;
        }
        if (i < lines.Length) i++; // skip closing fence
        return (string.Join('\n', content), i);
    }

    internal List<Command> ParseCommandsFromCodeBlock(string codeBlock, int blockStartLine)
    {
        var commands = new List<Command>();
        var blockLines = codeBlock.Split('\n');

        string? currentDescription = null;
        ToolMetadata? currentMetadata = null;
        var commandLines = new List<string>();
        var currentStartLine = blockStartLine;
        var inCommand = false;

        for (var j = 0; j < blockLines.Length; j++)
        {
            var line = blockLines[j].TrimEnd();

            if (string.IsNullOrWhiteSpace(line))
            {
                if (inCommand)
                {
                    FlushCommand(commands, commandLines, currentDescription, currentMetadata, currentStartLine, codeBlock);
                    currentDescription = null;
                    currentMetadata = null;
                    commandLines.Clear();
                    inCommand = false;
                }
                continue;
            }

            if (line.TrimStart().StartsWith('#'))
            {
                if (inCommand)
                {
                    FlushCommand(commands, commandLines, currentDescription, currentMetadata, currentStartLine, codeBlock);
                    commandLines.Clear();
                    inCommand = false;
                }

                var metaMatch = MetadataRegex().Match(line.TrimStart());
                if (metaMatch.Success)
                {
                    currentMetadata = new ToolMetadata
                    {
                        Destructive = metaMatch.Groups[1].Value == "✅",
                        Idempotent = metaMatch.Groups[2].Value == "✅",
                        OpenWorld = metaMatch.Groups[3].Value == "✅",
                        ReadOnly = metaMatch.Groups[4].Value == "✅",
                        Secret = metaMatch.Groups[5].Value == "✅",
                        LocalRequired = metaMatch.Groups[6].Value == "✅"
                    };
                }
                else
                {
                    // Description comment
                    var desc = line.TrimStart().TrimStart('#').Trim();
                    currentDescription = currentDescription == null ? desc : currentDescription + " " + desc;
                }
                continue;
            }

            if (CommandLineRegex().IsMatch(line.TrimStart()) || inCommand)
            {
                if (!inCommand)
                {
                    currentStartLine = blockStartLine + j;
                    inCommand = true;
                }
                commandLines.Add(line);

                // Check for line continuation
                if (!line.TrimEnd().EndsWith('\\'))
                {
                    FlushCommand(commands, commandLines, currentDescription, currentMetadata, currentStartLine, codeBlock);
                    currentDescription = null;
                    currentMetadata = null;
                    commandLines.Clear();
                    inCommand = false;
                }
            }
        }

        // Flush any remaining command
        if (commandLines.Count > 0)
            FlushCommand(commands, commandLines, currentDescription, currentMetadata, currentStartLine, codeBlock);

        return commands;
    }

    private void FlushCommand(List<Command> commands, List<string> commandLines, string? description, ToolMetadata? metadata, int sourceLine, string rawBlock)
    {
        if (commandLines.Count == 0) return;

        // Join continuation lines (remove trailing \ and join)
        var fullLine = string.Join(' ', commandLines.Select(l => l.TrimEnd().TrimEnd('\\')).Select(l => l.Trim()));

        if (!CommandLineRegex().IsMatch(fullLine)) return;

        var cmd = new Command
        {
            Description = description ?? string.Empty,
            Metadata = metadata,
            RawBlock = rawBlock,
            SourceLine = sourceLine
        };

        // Extract command text (azmcp <namespace> <subcommands>)
        var parts = TokenizeCommandLine(fullLine);
        if (parts.Count < 2) return;

        // parts[0] = "azmcp"
        cmd.Namespace = parts[1];

        // Find where parameters start (first token starting with -- or [)
        var paramStart = -1;
        for (var p = 2; p < parts.Count; p++)
        {
            if (parts[p].StartsWith("--") || parts[p].StartsWith("[--") || parts[p].StartsWith("["))
            {
                paramStart = p;
                break;
            }
        }

        if (paramStart > 0)
        {
            cmd.SubCommands = parts.GetRange(2, paramStart - 2);
            cmd.CommandText = string.Join(' ', parts.Take(paramStart));
        }
        else
        {
            cmd.SubCommands = parts.GetRange(2, parts.Count - 2);
            cmd.CommandText = string.Join(' ', parts);
        }

        // Parse parameters and alternative groups from the full line
        var (parameters, groups) = ParseParametersAndGroups(fullLine);
        cmd.Parameters = parameters;
        cmd.ParameterAlternativeGroups = groups;

        // Determine if this is an example (uses concrete values, not placeholders)
        cmd.IsExample = DetermineIfExample(fullLine);

        commands.Add(cmd);
    }

    internal (List<CommandParameter> Parameters, List<ParameterAlternativeGroup> Groups) ParseParametersAndGroups(string commandLine)
    {
        var groups = new List<ParameterAlternativeGroup>();

        // 1. Extract bracketed alternative groups [--a <a> | --b <b> --c <c>]
        var remaining = AlternativeGroupRegex().Replace(commandLine, match =>
        {
            var inner = match.Groups[1].Value;
            var alternatives = inner.Split('|');

            var group = new ParameterAlternativeGroup();
            foreach (var alt in alternatives)
            {
                group.Alternatives.Add(ParseFlatParameters(alt.Trim()));
            }

            // Only treat as a group if there are 2+ alternatives with params
            if (group.Alternatives.Count >= 2 && group.Alternatives.All(a => a.Count > 0))
            {
                groups.Add(group);
                return ""; // Remove from the line so flat parsing doesn't duplicate
            }

            // Single-alternative bracket (plain optional) — leave for flat parsing
            return match.Value;
        });

        // 2. Parse remaining non-group parameters
        var parameters = ParseFlatParameters(remaining);

        return (parameters, groups);
    }

    /// <summary>
    /// Parses individual parameters from a command line fragment (no group extraction).
    /// </summary>
    internal List<CommandParameter> ParseFlatParameters(string commandLine)
    {
        var parameters = new List<CommandParameter>();
        var matches = ParameterRegex().Matches(commandLine);

        foreach (Match match in matches)
        {
            var name = match.Groups["name"].Value;
            var value = match.Groups["value"].Value;
            var isOptional = match.Groups["optional"].Success || match.Value.TrimStart().StartsWith('[');
            var alias = match.Groups["alias"].Success ? match.Groups["alias"].Value : null;

            var param = new CommandParameter
            {
                Name = $"--{name}",
                ValuePlaceholder = value,
                IsRequired = !isOptional,
                IsFlag = string.IsNullOrEmpty(value),
                ShortAlias = alias != null ? $"--{alias}" : null
            };

            // Check for allowed values (pipe-separated in placeholder)
            if (!string.IsNullOrEmpty(value) && value.Contains('|') && !value.Contains(' '))
            {
                param.AllowedValues = value.Split('|').Select(v => v.Trim()).ToList();
            }

            parameters.Add(param);
        }

        return parameters;
    }

    /// <summary>
    /// Backward-compatible wrapper: returns only the flat parameter list.
    /// Grouped parameters are not included.
    /// </summary>
    internal List<CommandParameter> ParseParameters(string commandLine)
    {
        var (parameters, _) = ParseParametersAndGroups(commandLine);
        return parameters;
    }

    // ── Table parsing ────────────────────────────────────────────────────

    private bool IsTableHeaderRow(string[] lines, int i)
    {
        if (i + 1 >= lines.Length) return false;
        return lines[i].TrimStart().StartsWith('|') && TableSeparatorRegex().IsMatch(lines[i + 1]);
    }

    private (List<ParameterTableEntry>, int) ParseOptionTable(string[] lines, int i)
    {
        // Parse header to get column positions
        var headerCells = ParseTableCells(lines[i]);
        i += 2; // skip header + separator

        var entries = new List<ParameterTableEntry>();
        while (i < lines.Length && lines[i].TrimStart().StartsWith('|'))
        {
            var cells = ParseTableCells(lines[i]);
            if (cells.Count >= 2)
            {
                var entry = new ParameterTableEntry
                {
                    Name = CleanInlineCode(cells.ElementAtOrDefault(0) ?? ""),
                    IsRequired = (cells.ElementAtOrDefault(1) ?? "").Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase),
                    Default = cells.Count > 2 && headerCells.Any(h => h.Contains("Default", StringComparison.OrdinalIgnoreCase))
                        ? (cells.ElementAtOrDefault(2) ?? "").Trim()
                        : "",
                    Description = (cells.LastOrDefault() ?? "").Trim()
                };
                entries.Add(entry);
            }
            i++;
        }

        return (entries, i);
    }

    private (List<ParameterTableEntry>, int) ParseParameterTableEntries(string[] lines, int i)
    {
        return ParseOptionTable(lines, i);
    }

    private static List<string> ParseTableCells(string line)
    {
        var trimmed = line.Trim().Trim('|');
        return trimmed.Split('|').Select(c => c.Trim()).ToList();
    }

    // ── Utility methods ──────────────────────────────────────────────────

    internal static string DeriveAreaName(string heading)
    {
        // "Azure Storage Operations" → "storage"
        // "Azure Key Vault Operations" → "keyvault"
        // "Azure Container Registry (ACR) Operations" → "acr"
        // "Azure AI Search Operations" → "search"
        // "Azure Database for MySQL Operations" → "mysql"
        // "Bicep" → "bicep"
        // "Cloud Architect" → "cloudarchitect"
        // "Microsoft Foundry Operations" → "foundry"

        // Map of known headings to area names
        var knownMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Azure Advisor Operations"] = "advisor",
            ["Azure AI Search Operations"] = "search",
            ["Azure AI Services Speech Operations"] = "speech",
            ["Azure App Configuration Operations"] = "appconfig",
            ["Azure App Lens Operations"] = "applens",
            ["Azure Application Insights Operations"] = "applicationinsights",
            ["Azure App Service Operations"] = "appservice",
            ["Azure CLI Operations"] = "extension",
            ["Azure Communication Services Operations"] = "communication",
            ["Azure Compute Operations"] = "compute",
            ["Azure Confidential Ledger Operations"] = "confidentialledger",
            ["Azure Container Registry (ACR) Operations"] = "acr",
            ["Azure Cosmos DB Operations"] = "cosmos",
            ["Azure Data Explorer Operations"] = "kusto",
            ["Azure Database for MySQL Operations"] = "mysql",
            ["Azure Database for PostgreSQL Operations"] = "postgres",
            ["Azure Deploy Operations"] = "deploy",
            ["Azure Event Grid Operations"] = "eventgrid",
            ["Azure Event Hubs"] = "eventhubs",
            ["Azure File Shares Operations"] = "fileshares",
            ["Azure Function App Operations"] = "functionapp",
            ["Azure Key Vault Operations"] = "keyvault",
            ["Azure Kubernetes Service (AKS) Operations"] = "aks",
            ["Azure Load Testing Operations"] = "loadtesting",
            ["Azure Managed Grafana Operations"] = "grafana",
            ["Azure Marketplace Operations"] = "marketplace",
            ["Azure MCP Best Practices"] = "get",
            ["Azure MCP Tools"] = "tools",
            ["Azure Monitor Operations"] = "monitor",
            ["Azure Migrate Operations"] = "azuremigrate",
            ["Azure Managed Lustre Operations"] = "managedlustre",
            ["Azure Native ISV Operations"] = "datadog",
            ["Azure Quick Review CLI Operations"] = "extension",
            ["Azure Quota Operations"] = "quota",
            ["Azure Policy Operations"] = "policy",
            ["Azure Pricing Operations"] = "pricing",
            ["Azure RBAC Operations"] = "role",
            ["Azure Redis Operations"] = "redis",
            ["Azure Resource Group Operations"] = "group",
            ["Azure Resource Health Operations"] = "resourcehealth",
            ["Azure Service Bus Operations"] = "servicebus",
            ["Azure Service Fabric Operations"] = "servicefabric",
            ["Azure SignalR Service Operations"] = "signalr",
            ["Azure SQL Operations"] = "sql",
            ["Azure Storage Operations"] = "storage",
            ["Azure Storage Sync Operations"] = "storagesync",
            ["Azure Subscription Management"] = "subscription",
            ["Azure Terraform Best Practices"] = "azureterraformbestpractices",
            ["Azure Virtual Desktop Operations"] = "virtualdesktop",
            ["Azure Workbooks Operations"] = "workbooks",
            ["Bicep"] = "bicepschema",
            ["Cloud Architect"] = "cloudarchitect",
            ["Microsoft Foundry Operations"] = "foundry",
        };

        if (knownMappings.TryGetValue(heading, out var mapped))
            return mapped;

        // Fallback: try to derive from first command in section
        // Strip "Azure " prefix and " Operations" suffix, lowercase, remove spaces
        var name = heading
            .Replace("Azure ", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" Operations", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "")
            .ToLowerInvariant();

        return name;
    }

    private static bool DetermineIfExample(string commandLine)
    {
        // Examples use quoted concrete values like "my-subscription", "my-rg"
        // Definitions use <placeholder> syntax
        return commandLine.Contains('"') && !commandLine.Contains('<');
    }

    private static List<string> TokenizeCommandLine(string line)
    {
        // Simple tokenizer that respects quoted strings and <placeholders>
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        var inAngle = false;

        foreach (var ch in line)
        {
            switch (ch)
            {
                case '"':
                    inQuotes = !inQuotes;
                    current.Append(ch);
                    break;
                case '<':
                    inAngle = true;
                    current.Append(ch);
                    break;
                case '>':
                    inAngle = false;
                    current.Append(ch);
                    break;
                case ' ' or '\t' when !inQuotes && !inAngle:
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                    break;
                default:
                    current.Append(ch);
                    break;
            }
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens;
    }

    private static string CleanInlineCode(string text)
    {
        return text.Trim().Trim('`').Trim();
    }

    private static string JoinTrimmed(List<string> lines)
    {
        // Remove leading/trailing blank lines, preserve internal structure
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
            lines.RemoveAt(0);
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);
        return string.Join('\n', lines);
    }
}
