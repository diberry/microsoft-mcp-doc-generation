// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using System.Text.Json;
using CSharpGenerator.Models;
using HorizontalArticleGenerator.Generators;
using HorizontalArticleGenerator.Models;
using Shared;

namespace HorizontalArticleGenerator.Builders;

/// <summary>
/// Builder that deterministically assembles the minimal evidence pack for the HorizontalArticles
/// AI stage, producing a typed <see cref="ArticleOutlineContext"/> step envelope containing the
/// article title, ordered sections, and per-section evidence items.
/// </summary>
public sealed class ArticleOutlineBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> CommonParameters = new(StringComparer.OrdinalIgnoreCase)
    {
        "subscription-id",
        "resource-group",
        "output",
        "verbose",
        "help",
        "debug",
        "only-show-errors",
        "tenant",
        "auth-method",
        "retry-delay",
        "retry-max-delay",
        "retry-max-retries",
        "retry-mode",
        "retry-network-timeout"
    };

    public async Task<ArticleOutlineContext> BuildAsync(string outputPath, string serviceNamespace, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceNamespace);
        ct.ThrowIfCancellationRequested();

        var normalizedNamespace = NormalizeNamespace(serviceNamespace);
        var brandMappings = await DataFileLoader.LoadBrandMappingsAsync();
        var brandMapping = ResolveBrandMapping(brandMappings, normalizedNamespace);
        var toolFamilyFileName = ResolveToolFamilyFileName(brandMapping, normalizedNamespace);
        var tools = await LoadToolsAsync(outputPath, normalizedNamespace, ct);
        var sections = BuildSections(toolFamilyFileName, tools);

        return new ArticleOutlineContext(
            ResolveArticleTitle(brandMapping, normalizedNamespace),
            sections,
            normalizedNamespace);
    }

    private static async Task<IReadOnlyList<OutlineTool>> LoadToolsAsync(string outputPath, string serviceNamespace, CancellationToken ct)
    {
        var cliOutputPath = Path.Combine(outputPath, "cli", "cli-output.json");
        if (!File.Exists(cliOutputPath))
        {
            return [];
        }

        var cliRawJson = await File.ReadAllTextAsync(cliOutputPath, ct);
        var cliOutput = JsonSerializer.Deserialize<CliOutput>(cliRawJson, JsonOptions);
        if (cliOutput?.Results is null)
        {
            return [];
        }

        var tools = cliOutput.Results
            .Where(tool => IsMatchingNamespace(tool, serviceNamespace))
            .Select(CreateOutlineTool)
            .ToList();

        if (tools.Count == 0)
        {
            return [];
        }

        var orderedToolSummaries = DeterministicHorizontalHelpers.OrderToolsByPlane(
            tools.Select(tool => tool.ToSummary()).ToList());
        var orderLookup = orderedToolSummaries
            .Select((tool, index) => new { tool.Command, index })
            .ToDictionary(item => item.Command, item => item.index, StringComparer.OrdinalIgnoreCase);

        return tools
            .OrderBy(tool => orderLookup.GetValueOrDefault(tool.Command, int.MaxValue))
            .ThenBy(tool => tool.Command, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<ArticleOutlineSection> BuildSections(string toolFamilyFileName, IReadOnlyList<OutlineTool> tools)
    {
        var toolFamilyReference = $"xref:../tool-family/{toolFamilyFileName}.md";
        var introductionEvidence = new List<string> { toolFamilyReference };
        foreach (var plane in tools.Select(tool => tool.Plane).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            introductionEvidence.Add($"capability:{plane}");
        }

        var prerequisitesEvidence = new List<string> { toolFamilyReference };
        foreach (var parameterLink in tools
                     .Where(tool => tool.Secret || tool.ParameterCount > 0)
                     .Select(tool => $"xref:{tool.MoreInfoLink}")
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            prerequisitesEvidence.Add(parameterLink);
        }

        if (tools.Any(tool => tool.Secret))
        {
            prerequisitesEvidence.Add("capability:secret");
        }

        if (tools.Any(tool => tool.LocalRequired))
        {
            prerequisitesEvidence.Add("capability:local-required");
        }

        var toolOverviewEvidence = tools
            .Select(tool => JsonSerializer.Serialize(new
            {
                kind = "tool",
                command = tool.Command,
                description = tool.Description,
                parameterCount = tool.ParameterCount,
                moreInfoLink = tool.MoreInfoLink,
                destructive = tool.Destructive,
                readOnly = tool.ReadOnly,
                secret = tool.Secret,
                plane = tool.Plane
            }))
            .ToArray();

        var scenarioEvidence = tools
            .Take(4)
            .SelectMany(tool => new[]
            {
                $"scenario-tool:{tool.Command}",
                $"capability:{tool.Plane}"
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var bestPracticesEvidence = new List<string> { toolFamilyReference };
        if (tools.Any(tool => tool.Destructive))
        {
            bestPracticesEvidence.Add("capability:destructive");
        }

        if (tools.Any(tool => tool.Secret))
        {
            bestPracticesEvidence.Add("capability:secret");
        }

        if (tools.Any(tool => tool.ReadOnly))
        {
            bestPracticesEvidence.Add("capability:read-only");
        }

        return
        [
            new ArticleOutlineSection("Introduction", "overview", introductionEvidence),
            new ArticleOutlineSection("Prerequisites", "checklist", prerequisitesEvidence),
            new ArticleOutlineSection("Tool overview", "reference", toolOverviewEvidence),
            new ArticleOutlineSection("Common scenarios", "scenario-list", scenarioEvidence),
            new ArticleOutlineSection("Best practices", "guidance", bestPracticesEvidence)
        ];
    }

    private static OutlineTool CreateOutlineTool(Tool tool)
    {
        var command = (tool.Command ?? tool.Name ?? string.Empty).Trim();
        var description = (tool.Description ?? string.Empty).Trim();
        var parameterCount = tool.Option?.Count(option => !CommonParameters.Contains(option.Name ?? string.Empty)) ?? 0;

        return new OutlineTool(
            command,
            description,
            parameterCount,
            $"../parameters/{command.Replace(' ', '-')}-parameters.md",
            tool.Metadata?.Destructive?.Value ?? false,
            tool.Metadata?.ReadOnly?.Value ?? false,
            tool.Metadata?.Secret?.Value ?? false,
            tool.Metadata?.LocalRequired?.Value ?? false,
            ClassifyPlane(command, description, tool.Metadata));
    }

    private static string ClassifyPlane(string command, string description, ToolMetadata? metadata)
    {
        var summary = new HorizontalToolSummary
        {
            Command = command,
            Description = description,
            Metadata = new Dictionary<string, MetadataValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["destructive"] = new MetadataValue { Value = metadata?.Destructive?.Value ?? false },
                ["readOnly"] = new MetadataValue { Value = metadata?.ReadOnly?.Value ?? false },
                ["secret"] = new MetadataValue { Value = metadata?.Secret?.Value ?? false }
            }
        };

        return DeterministicHorizontalHelpers.ClassifyToolPlane(summary);
    }

    private static bool IsMatchingNamespace(Tool tool, string serviceNamespace)
    {
        var command = NormalizeNamespace(tool.Command ?? tool.Name ?? string.Empty);
        return string.Equals(command, serviceNamespace, StringComparison.OrdinalIgnoreCase)
            || command.StartsWith($"{serviceNamespace} ", StringComparison.OrdinalIgnoreCase);
    }

    private static BrandMapping? ResolveBrandMapping(IReadOnlyDictionary<string, BrandMapping> brandMappings, string serviceNamespace)
    {
        if (brandMappings.TryGetValue(serviceNamespace, out var exact))
        {
            return exact;
        }

        var underscoredNamespace = serviceNamespace.Replace(' ', '_');
        return brandMappings.TryGetValue(underscoredNamespace, out var underscored) ? underscored : null;
    }

    private static string ResolveArticleTitle(BrandMapping? brandMapping, string serviceNamespace)
    {
        if (!string.IsNullOrWhiteSpace(brandMapping?.BrandName))
        {
            return brandMapping.BrandName!;
        }

        var words = serviceNamespace
            .Replace('_', ' ')
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(static word => CultureInfo.InvariantCulture.TextInfo.ToTitleCase(word));
        return string.Join(' ', words);
    }

    private static string ResolveToolFamilyFileName(BrandMapping? brandMapping, string serviceNamespace)
    {
        if (!string.IsNullOrWhiteSpace(brandMapping?.FileName))
        {
            return brandMapping.FileName!;
        }

        return serviceNamespace.Replace(' ', '-').ToLowerInvariant();
    }

    private static string NormalizeNamespace(string value)
        => value.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Trim()
            .Replace('_', ' ')
            .ToLowerInvariant();

    private sealed record OutlineTool(
        string Command,
        string Description,
        int ParameterCount,
        string MoreInfoLink,
        bool Destructive,
        bool ReadOnly,
        bool Secret,
        bool LocalRequired,
        string Plane)
    {
        public HorizontalToolSummary ToSummary() =>
            new()
            {
                Command = Command,
                Description = Description,
                ParameterCount = ParameterCount,
                MoreInfoLink = MoreInfoLink,
                Metadata = new Dictionary<string, MetadataValue>(StringComparer.OrdinalIgnoreCase)
                {
                    ["destructive"] = new MetadataValue { Value = Destructive },
                    ["readOnly"] = new MetadataValue { Value = ReadOnly },
                    ["secret"] = new MetadataValue { Value = Secret }
                }
            };
    }
}
