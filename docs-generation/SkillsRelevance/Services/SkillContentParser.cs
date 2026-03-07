// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.RegularExpressions;
using SkillsRelevance.Models;

namespace SkillsRelevance.Services;

/// <summary>
/// Parses skill file content to extract metadata.
/// Supports YAML frontmatter in Markdown files, plain YAML, and JSON formats.
/// </summary>
public static class SkillContentParser
{
    private static readonly Regex FrontmatterRegex = new(@"^---\s*\n(.*?)\n---", RegexOptions.Singleline | RegexOptions.Compiled);

    // Known Azure services for extraction
    private static readonly string[] KnownAzureServices =
    [
        "Azure Storage", "Azure Blob Storage", "Azure Kubernetes Service", "AKS",
        "Azure Container Registry", "ACR", "Azure App Service", "Azure Functions",
        "Azure SQL", "Azure Cosmos DB", "Azure Key Vault", "Azure Active Directory",
        "Microsoft Entra", "Azure DevOps", "Azure Monitor", "Azure Log Analytics",
        "Azure Virtual Machines", "Azure Network", "Azure OpenAI", "Azure AI",
        "Azure Machine Learning", "Azure Service Bus", "Azure Event Hub",
        "Azure Resource Manager", "ARM", "Azure CLI", "Azure Portal",
        "Azure Static Web Apps", "Azure Container Apps", "Azure Spring Apps",
        "Azure Cache for Redis", "Azure API Management", "Azure Logic Apps",
        "Azure Data Factory", "Azure Synapse", "Azure Databricks",
        "GitHub Actions", "GitHub Copilot", "Visual Studio Code"
    ];

    /// <summary>
    /// Parses a skill file's content and returns populated SkillInfo fields.
    /// </summary>
    public static SkillInfo Parse(string fileName, string content, string sourceUrl, string rawContentUrl, string sourceRepository)
    {
        var skill = new SkillInfo
        {
            FileName = fileName,
            SourceUrl = sourceUrl,
            RawContentUrl = rawContentUrl,
            SourceRepository = sourceRepository,
            RawContent = content
        };

        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        if (extension == ".json")
        {
            ParseJson(skill, content);
        }
        else if (extension == ".yml" || extension == ".yaml")
        {
            ParseYaml(skill, content);
        }
        else // .md
        {
            ParseMarkdown(skill, content);
        }

        // Post-process: derive name from filename if not set
        if (string.IsNullOrEmpty(skill.Name))
        {
            skill.Name = Path.GetFileNameWithoutExtension(fileName)
                .Replace("-", " ")
                .Replace("_", " ");
        }

        // Extract Azure services from content if not already set
        if (skill.AzureServices.Count == 0)
        {
            skill.AzureServices = ExtractAzureServices(content);
        }

        // Extract best practices and troubleshooting from markdown sections
        if (string.IsNullOrEmpty(skill.BestPractices))
        {
            skill.BestPractices = ExtractSection(content, "best practice");
        }

        if (string.IsNullOrEmpty(skill.Troubleshooting))
        {
            skill.Troubleshooting = ExtractSection(content, "troubleshoot");
        }

        return skill;
    }

    private static void ParseMarkdown(SkillInfo skill, string content)
    {
        // Extract YAML frontmatter
        var frontmatterMatch = FrontmatterRegex.Match(content);
        if (frontmatterMatch.Success)
        {
            var frontmatter = frontmatterMatch.Groups[1].Value;
            ExtractYamlFields(skill, frontmatter);

            // Body is everything after frontmatter
            var bodyStart = frontmatterMatch.Length;
            var body = content[bodyStart..].TrimStart();
            ExtractMarkdownBody(skill, body);
        }
        else
        {
            ExtractMarkdownBody(skill, content);
        }
    }

    private static void ExtractMarkdownBody(SkillInfo skill, string body)
    {
        // Extract title from first H1 if name not set
        if (string.IsNullOrEmpty(skill.Name))
        {
            var h1Match = Regex.Match(body, @"^#\s+(.+)$", RegexOptions.Multiline);
            if (h1Match.Success)
            {
                skill.Name = h1Match.Groups[1].Value.Trim();
            }
        }

        // Extract description from first paragraph if not set
        if (string.IsNullOrEmpty(skill.Description))
        {
            var lines = body.Split('\n');
            var descLines = new List<string>();
            bool inFrontSection = true;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith('#')) { inFrontSection = false; continue; }
                if (inFrontSection && !string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("<!--"))
                {
                    descLines.Add(trimmed);
                    if (descLines.Count >= 3) break;
                }
            }
            if (descLines.Count > 0)
            {
                skill.Description = string.Join(" ", descLines);
            }
        }

        // Set purpose from description if not set
        if (string.IsNullOrEmpty(skill.Purpose) && !string.IsNullOrEmpty(skill.Description))
        {
            skill.Purpose = skill.Description;
        }
    }

    private static void ParseYaml(SkillInfo skill, string content)
    {
        ExtractYamlFields(skill, content);
    }

    private static void ExtractYamlFields(SkillInfo skill, string yaml)
    {
        // Simple line-by-line YAML parsing for common fields
        string? currentKey = null;
        var listValues = new List<string>();

        foreach (var line in yaml.Split('\n'))
        {
            var trimmed = line.TrimEnd();

            // Check if this is a list item under the current key
            var listMatch = Regex.Match(trimmed, @"^\s+-\s+(.+)$");
            if (listMatch.Success && currentKey != null)
            {
                listValues.Add(listMatch.Groups[1].Value.Trim().Trim('"', '\''));
                continue;
            }

            // Check for key: value
            var kvMatch = Regex.Match(trimmed, @"^(\w[\w\-_]*):\s*(.*)$");
            if (kvMatch.Success)
            {
                // Save previous list
                if (currentKey != null && listValues.Count > 0)
                {
                    ApplyYamlList(skill, currentKey, listValues);
                    listValues.Clear();
                }

                currentKey = kvMatch.Groups[1].Value.ToLowerInvariant();
                var value = kvMatch.Groups[2].Value.Trim().Trim('"', '\'');

                if (!string.IsNullOrEmpty(value))
                {
                    ApplyYamlValue(skill, currentKey, value);
                    currentKey = null; // Not expecting list items for this key
                }
            }
        }

        // Apply any remaining list
        if (currentKey != null && listValues.Count > 0)
        {
            ApplyYamlList(skill, currentKey, listValues);
        }
    }

    private static void ApplyYamlValue(SkillInfo skill, string key, string value)
    {
        switch (key)
        {
            case "name":
            case "title":
                if (string.IsNullOrEmpty(skill.Name)) skill.Name = value;
                break;
            case "description":
            case "summary":
                if (string.IsNullOrEmpty(skill.Description)) skill.Description = value;
                break;
            case "purpose":
            case "goal":
                if (string.IsNullOrEmpty(skill.Purpose)) skill.Purpose = value;
                break;
            case "author":
            case "owner":
                if (string.IsNullOrEmpty(skill.Author)) skill.Author = value;
                break;
            case "version":
                if (string.IsNullOrEmpty(skill.Version)) skill.Version = value;
                break;
            case "category":
            case "type":
                if (string.IsNullOrEmpty(skill.Category)) skill.Category = value;
                break;
            case "date":
            case "last_modified":
            case "updated":
                if (!skill.LastUpdated.HasValue && DateTimeOffset.TryParse(value, out var date))
                {
                    skill.LastUpdated = date;
                }
                break;
        }
    }

    private static void ApplyYamlList(SkillInfo skill, string key, List<string> values)
    {
        switch (key)
        {
            case "tags":
            case "labels":
                skill.Tags.AddRange(values);
                break;
            case "azure_services":
            case "services":
            case "azure-services":
                skill.AzureServices.AddRange(values);
                break;
        }
    }

    private static void ParseJson(SkillInfo skill, string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;

            TrySetStringField(root, "name", v => skill.Name = v);
            TrySetStringField(root, "title", v => { if (string.IsNullOrEmpty(skill.Name)) skill.Name = v; });
            TrySetStringField(root, "description", v => skill.Description = v);
            TrySetStringField(root, "purpose", v => skill.Purpose = v);
            TrySetStringField(root, "author", v => skill.Author = v);
            TrySetStringField(root, "version", v => skill.Version = v);
            TrySetStringField(root, "category", v => skill.Category = v);

            if (root.TryGetProperty("tags", out var tags) && tags.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tags.EnumerateArray())
                {
                    if (tag.ValueKind == JsonValueKind.String)
                        skill.Tags.Add(tag.GetString()!);
                }
            }

            if (root.TryGetProperty("services", out var services) && services.ValueKind == JsonValueKind.Array)
            {
                foreach (var svc in services.EnumerateArray())
                {
                    if (svc.ValueKind == JsonValueKind.String)
                        skill.AzureServices.Add(svc.GetString()!);
                }
            }
        }
        catch
        {
            // If JSON parsing fails, treat as plain text
            if (string.IsNullOrEmpty(skill.Description))
                skill.Description = content.Length > 200 ? content[..200] + "..." : content;
        }
    }

    private static void TrySetStringField(JsonElement root, string propertyName, Action<string> setter)
    {
        if (root.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            var value = prop.GetString();
            if (!string.IsNullOrEmpty(value))
                setter(value);
        }
    }

    /// <summary>
    /// Extracts known Azure service names mentioned in the content.
    /// </summary>
    public static List<string> ExtractAzureServices(string content)
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var service in KnownAzureServices)
        {
            if (content.Contains(service, StringComparison.OrdinalIgnoreCase))
            {
                found.Add(service);
            }
        }

        return found.OrderBy(s => s).ToList();
    }

    /// <summary>
    /// Extracts a markdown section by heading keyword.
    /// </summary>
    public static string ExtractSection(string content, string headingKeyword)
    {
        var lines = content.Split('\n');
        var inSection = false;
        var sectionLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith('#'))
            {
                if (inSection)
                {
                    // We've hit a new heading — stop collecting
                    break;
                }
                if (trimmed.Contains(headingKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    inSection = true;
                    continue;
                }
            }
            else if (inSection)
            {
                sectionLines.Add(line);
            }
        }

        return string.Join("\n", sectionLines).Trim();
    }
}
