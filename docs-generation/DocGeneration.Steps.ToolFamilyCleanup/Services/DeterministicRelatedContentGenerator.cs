// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Generates "Related content" section deterministically using a JSON lookup
/// instead of an AI call. Eliminates 1 AI call per namespace (~52 total).
/// Fixes: #163 Tier 1a
///
/// Output format matches what the AI previously generated:
///   ## Related content
///   - [What are the Azure MCP Server tools?](index.md)
///   - [Get started using Azure MCP Server](../get-started.md)
///   - [{Service} documentation]({url})
/// </summary>
public static class DeterministicRelatedContentGenerator
{
    /// <summary>
    /// Generates related content section for a namespace.
    /// </summary>
    /// <param name="familyName">The MCP namespace (e.g., "storage", "keyvault")</param>
    /// <param name="serviceDocLinks">Lookup table of namespace → doc link</param>
    /// <returns>Markdown related content section</returns>
    public static string Generate(string familyName, IReadOnlyDictionary<string, ServiceDocLink> serviceDocLinks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Related content");
        sb.AppendLine();
        sb.AppendLine("- [What are the Azure MCP Server tools?](index.md)");
        sb.AppendLine("- [Get started using Azure MCP Server](../get-started.md)");

        if (serviceDocLinks.TryGetValue(familyName, out var link))
        {
            sb.AppendLine($"- [{link.Title}]({link.Url})");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Loads service doc links from a JSON file.
    /// </summary>
    public static Dictionary<string, ServiceDocLink> LoadServiceDocLinks(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<Dictionary<string, ServiceDocLink>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new Dictionary<string, ServiceDocLink>();
    }
}

/// <summary>
/// Represents a link to Azure service documentation.
/// </summary>
public record ServiceDocLink(string Title, string Url);
