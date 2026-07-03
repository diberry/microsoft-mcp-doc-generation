// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using ToolFamilyCleanup.Models;
using Shared;

namespace ToolFamilyCleanup.Services;

/// <summary>
/// Builder that deterministically assembles the canonical <see cref="FamilyStructureContext"/>
/// step envelope from staged tool files, emitting family name, section order, H2 headings,
/// and source content so the ToolFamilyCleanup AI stage handles prose only.
/// </summary>
public sealed class FamilyStructureBuilder
{
    public async Task<FamilyStructureContext> BuildAsync(
        string toolsDirectory,
        string familyName,
        string? h2HeadingsDirectory,
        CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolsDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(familyName);
        ct.ThrowIfCancellationRequested();

        if (!Directory.Exists(toolsDirectory))
        {
            return new FamilyStructureContext(familyName, []);
        }

        var toolReader = new ToolReader(toolsDirectory);
        var toolsByFamily = await toolReader.ReadAndGroupToolsAsync();
        var tools = toolsByFamily
            .FirstOrDefault(kv => string.Equals(kv.Key, familyName, StringComparison.OrdinalIgnoreCase))
            .Value;

        if (tools is null || tools.Count == 0)
        {
            return new FamilyStructureContext(familyName, []);
        }

        var headings = await LoadHeadingsAsync(h2HeadingsDirectory, familyName, ct);
        var compoundWords = await DataFileLoader.LoadCompoundWordsAsync();
        var sections = BuildSections(tools, headings, compoundWords);
        return new FamilyStructureContext(familyName, sections);
    }

    private static IReadOnlyList<FamilySection> BuildSections(
        IReadOnlyList<ToolContent> tools,
        IReadOnlyDictionary<string, string> headings,
        Dictionary<string, string> compoundWords)
    {
        var orderedTools = tools
            .OrderBy(tool => tool.FileName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(tool => tool.FileName, StringComparer.Ordinal)
            .ToList();

        var isMultiResource = FamilyFileStitcher.IsMultiResourceFamily(orderedTools);
        return isMultiResource
            ? BuildMultiResourceSections(orderedTools, headings, compoundWords)
            : BuildSingleResourceSections(orderedTools, headings, compoundWords);
    }

    private static IReadOnlyList<FamilySection> BuildSingleResourceSections(
        IReadOnlyList<ToolContent> tools,
        IReadOnlyDictionary<string, string> headings,
        Dictionary<string, string> compoundWords)
    {
        foreach (var tool in tools)
        {
            tool.ToolName = ResolveBaseHeading(tool, headings, compoundWords);
        }

        return ToolOrderingPolicy.OrderForSingleResource(tools)
            .Select(tool => CreateSection(tool, tool.ToolName))
            .ToArray();
    }

    private static IReadOnlyList<FamilySection> BuildMultiResourceSections(
        IReadOnlyList<ToolContent> tools,
        IReadOnlyDictionary<string, string> headings,
        Dictionary<string, string> compoundWords)
    {
        var sections = new List<FamilySection>();
        var groupOrder = tools
            .Select(tool => tool.ResourceType ?? string.Empty)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var resourceType in groupOrder)
        {
            var resourceTools = tools
                .Where(tool => string.Equals(tool.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var tool in ToolOrderingPolicy.OrderForMultiResource(resourceTools))
            {
                var heading = ResolveMultiResourceHeading(tool, headings, compoundWords);
                sections.Add(CreateSection(tool, heading));
            }
        }

        return sections;
    }

    private static FamilySection CreateSection(ToolContent tool, string heading)
    {
        var command = DeriveCommand(tool);
        var sourceContent = EnsureH2Heading(CleanupGenerator.ReplaceH2Heading(tool.Content, heading), heading);
        return new FamilySection(
            heading,
            string.IsNullOrWhiteSpace(command) ? [] : [command],
            sourceContent);
    }

    private static string ResolveMultiResourceHeading(
        ToolContent tool,
        IReadOnlyDictionary<string, string> headings,
        Dictionary<string, string> compoundWords)
    {
        var command = DeriveCommand(tool);
        if (!string.IsNullOrWhiteSpace(command))
        {
            var overrideHeading = HeadingOverrideProvider.GetOverride(command);
            if (!string.IsNullOrWhiteSpace(overrideHeading))
            {
                return overrideHeading;
            }

            var action = ToolOrderingPolicy.ExtractActionVerb(command);
            if (!string.IsNullOrWhiteSpace(tool.ResourceType) && !string.IsNullOrWhiteSpace(action))
            {
                return MultiResourceH2Formatter.FormatToolHeading(tool.ResourceType, action);
            }
        }

        return ResolveBaseHeading(tool, headings, compoundWords);
    }

    private static string ResolveBaseHeading(
        ToolContent tool,
        IReadOnlyDictionary<string, string> headings,
        Dictionary<string, string> compoundWords)
    {
        var command = DeriveCommand(tool);
        if (!string.IsNullOrWhiteSpace(command) && headings.TryGetValue(command, out var configuredHeading))
        {
            return configuredHeading;
        }

        if (!string.IsNullOrWhiteSpace(command))
        {
            return DeterministicH2HeadingGenerator.GenerateHeading(command, tool.Description, compoundWords);
        }

        return DeriveHeadingFromFileName(tool.FileName, tool.FamilyName);
    }

    private static string DeriveCommand(ToolContent tool)
    {
        if (!string.IsNullOrWhiteSpace(tool.Command))
        {
            return tool.Command;
        }

        var fileName = Path.GetFileNameWithoutExtension(tool.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        var normalized = fileName
            .Replace("azure-", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace('-', ' ')
            .Trim();

        return normalized;
    }

    private static string DeriveHeadingFromFileName(string fileName, string familyName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(stem))
        {
            return "Tool";
        }

        var prefixes = new[]
        {
            $"{familyName}-",
            $"azure-{familyName}-",
            $"ai-{familyName}-"
        };

        foreach (var prefix in prefixes)
        {
            if (stem.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                stem = stem[prefix.Length..];
                break;
            }
        }

        var words = stem
            .Split('-', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word))
            .ToArray();

        return words.Length == 0 ? "Tool" : string.Join(' ', words);
    }

    private static string EnsureH2Heading(string content, string heading)
    {
        if (content.StartsWith("## ", StringComparison.Ordinal))
        {
            return content;
        }

        return $"## {heading}{Environment.NewLine}{Environment.NewLine}{content.TrimStart()}";
    }

    private static async Task<IReadOnlyDictionary<string, string>> LoadHeadingsAsync(
        string? h2HeadingsDirectory,
        string familyName,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(h2HeadingsDirectory) || !Directory.Exists(h2HeadingsDirectory))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        var path = Path.Combine(h2HeadingsDirectory, $"{familyName}.json");
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        await using var stream = File.OpenRead(path);
        var headings = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, cancellationToken: ct);
        return headings ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
