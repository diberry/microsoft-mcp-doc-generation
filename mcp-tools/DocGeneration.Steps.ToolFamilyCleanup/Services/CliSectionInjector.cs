// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Reads CLI enriched JSON and injects #### CLI subsections into tool content
/// after the tool annotation hints line.
/// </summary>
public class CliSectionInjector
{
    // Common/infrastructure params excluded from per-tool CLI tables
    private static readonly HashSet<string> ExcludedParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "--tenant",
        "--auth-method",
        "--retry-delay",
        "--retry-max-delay",
        "--retry-max-retries",
        "--retry-mode",
        "--retry-network-timeout",
        "--subscription",
        "--learn"
    };

    // Matches the annotation values line: "Destructive: ✅ | Idempotent: ❌ | ..."
    private static readonly Regex AnnotationValuesRegex = new(
        @"^Destructive:\s*[✅❌].+Local Required:\s*[✅❌]\s*$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Loads CLI commands from the enriched JSON file.
    /// Handles BOM-encoded UTF-8.
    /// </summary>
    public static async Task<Dictionary<string, CliCommand>> LoadCliCommandsAsync(string cliEnrichedJsonPath)
    {
        if (!File.Exists(cliEnrichedJsonPath))
        {
            Console.WriteLine($"⚠ CLI enriched JSON not found at '{cliEnrichedJsonPath}'. CLI sections will be skipped.");
            return new Dictionary<string, CliCommand>(StringComparer.OrdinalIgnoreCase);
        }

        var jsonBytes = await File.ReadAllBytesAsync(cliEnrichedJsonPath);
        // Skip BOM if present
        var json = Encoding.UTF8.GetString(jsonBytes).TrimStart('\uFEFF');

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var envelope = JsonSerializer.Deserialize<CliEnrichedEnvelope>(json, options);
        if (envelope?.Results == null || envelope.Results.Count == 0)
        {
            Console.WriteLine("⚠ CLI enriched JSON has no results.");
            return new Dictionary<string, CliCommand>(StringComparer.OrdinalIgnoreCase);
        }

        var dict = new Dictionary<string, CliCommand>(StringComparer.OrdinalIgnoreCase);
        foreach (var cmd in envelope.Results)
        {
            if (!string.IsNullOrWhiteSpace(cmd.Command))
            {
                dict[cmd.Command] = cmd;
            }
        }

        Console.WriteLine($"✓ Loaded {dict.Count} CLI commands from enriched JSON");
        return dict;
    }

    /// <summary>
    /// Injects a #### CLI subsection into tool content after the annotation hints line.
    /// Returns the modified content, or the original if no matching CLI command exists.
    /// </summary>
    public static string InjectCliSection(string toolContent, string? command, Dictionary<string, CliCommand> cliCommands)
    {
        if (string.IsNullOrWhiteSpace(command) || cliCommands.Count == 0)
            return toolContent;

        if (!cliCommands.TryGetValue(command, out var cliCmd))
            return toolContent;

        var cliSection = BuildCliSection(command, cliCmd);
        if (string.IsNullOrEmpty(cliSection))
            return toolContent;

        // Find the annotation values line and insert after it
        var match = AnnotationValuesRegex.Match(toolContent);
        if (!match.Success)
        {
            // Fallback: append to end of content
            return toolContent.TrimEnd() + "\n\n" + cliSection;
        }

        var insertIndex = match.Index + match.Length;
        return toolContent[..insertIndex] + "\n\n" + cliSection + toolContent[insertIndex..];
    }

    /// <summary>
    /// Builds the #### CLI markdown section for a tool.
    /// </summary>
    internal static string BuildCliSection(string command, CliCommand cliCmd)
    {
        var sb = new StringBuilder();

        // #### CLI heading
        sb.AppendLine("#### CLI");
        sb.AppendLine();

        // Description paragraph
        var description = (cliCmd.Description ?? "").Trim().Replace("\r\n", " ").Replace("\n", " ");
        sb.AppendLine(description);
        sb.AppendLine();

        // Filter to domain-specific params only
        var domainParams = (cliCmd.Option ?? [])
            .Where(o => !string.IsNullOrWhiteSpace(o.Name) && !ExcludedParams.Contains(o.Name))
            .ToList();

        // Build CLI command syntax
        var requiredParams = domainParams.Where(o => o.Required == true).ToList();
        var optionalParams = domainParams.Where(o => o.Required != true).ToList();

        sb.AppendLine("```bash");
        sb.Append($"azmcp {command}");

        if (requiredParams.Count > 0 || optionalParams.Count > 0)
        {
            foreach (var param in requiredParams)
            {
                var placeholder = GetPlaceholder(param, cliCmd);
                sb.Append($" \\\n{Indent(command)} {param.Name} <{placeholder}>");
            }
            foreach (var param in optionalParams)
            {
                var placeholder = GetPlaceholder(param, cliCmd);
                sb.Append($" \\\n{Indent(command)} [{param.Name} <{placeholder}>]");
            }
        }

        sb.AppendLine();
        sb.AppendLine("```");

        // Parameter table (only if there are domain params)
        if (domainParams.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("| Switch | Required | Type | Description |");
            sb.AppendLine("|--------|----------|------|-------------|");

            foreach (var param in domainParams)
            {
                var required = param.Required == true ? "✅" : "❌";
                var type = param.Type ?? "string";
                var desc = (param.Description ?? "").Trim().Replace("\r\n", " ").Replace("\n", " ");
                sb.AppendLine($"| `{param.Name}` | {required} | {type} | {desc} |");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static string GetPlaceholder(CliOption option, CliCommand cliCmd)
    {
        // Try enrichment valuePlaceholder first
        if (cliCmd.Enrichment?.ParameterEnhancements != null &&
            cliCmd.Enrichment.ParameterEnhancements.TryGetValue(option.Name!, out var enhancement) &&
            !string.IsNullOrWhiteSpace(enhancement.ValuePlaceholder))
        {
            return enhancement.ValuePlaceholder;
        }

        // Fallback: derive from switch name (strip leading --)
        return option.Name!.TrimStart('-');
    }

    /// <summary>
    /// Creates indentation to align continuation lines under the first parameter.
    /// </summary>
    private static string Indent(string command)
    {
        // "azmcp " (6) + command length
        return new string(' ', 6 + command.Length);
    }
}

// JSON model classes for CLI enriched data

public class CliEnrichedEnvelope
{
    [JsonPropertyName("results")]
    public List<CliCommand> Results { get; set; } = [];
}

public class CliCommand
{
    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("option")]
    public List<CliOption>? Option { get; set; }

    [JsonPropertyName("enrichment")]
    public CliEnrichment? Enrichment { get; set; }
}

public class CliOption
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("required")]
    public bool? Required { get; set; }
}

public class CliEnrichment
{
    [JsonPropertyName("parameterEnhancements")]
    public Dictionary<string, CliParameterEnhancement>? ParameterEnhancements { get; set; }
}

public class CliParameterEnhancement
{
    [JsonPropertyName("valuePlaceholder")]
    public string? ValuePlaceholder { get; set; }

    [JsonPropertyName("default")]
    public string? Default { get; set; }
}
