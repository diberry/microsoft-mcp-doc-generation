using System.Text.Json;
using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Models;

namespace DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Services;

public sealed class EnrichmentOrchestrator
{
    private readonly ToolMatcher _toolMatcher;
    private readonly ConditionalParamExtractor _conditionalParamExtractor;
    private readonly ParameterEnricher _parameterEnricher;

    public EnrichmentOrchestrator(
        ToolMatcher toolMatcher,
        ConditionalParamExtractor conditionalParamExtractor,
        ParameterEnricher parameterEnricher)
    {
        _toolMatcher = toolMatcher ?? throw new ArgumentNullException(nameof(toolMatcher));
        _conditionalParamExtractor = conditionalParamExtractor ?? throw new ArgumentNullException(nameof(conditionalParamExtractor));
        _parameterEnricher = parameterEnricher ?? throw new ArgumentNullException(nameof(parameterEnricher));
    }

    public EnrichedCliOutputDocument Enrich(CliOutputDocument cliOutputDocument)
    {
        ArgumentNullException.ThrowIfNull(cliOutputDocument);

        var results = new List<EnrichedCliOutputTool>(cliOutputDocument.Results.Count);
        var matchedTools = 0;
        var conditionalGroupsFound = 0;

        foreach (var tool in cliOutputDocument.Results)
        {
            var matchedCommand = _toolMatcher.Match(tool);
            if (matchedCommand is not null)
            {
                matchedTools++;
            }

            var conditionalGroups = _conditionalParamExtractor.Extract(tool.Description);
            conditionalGroupsFound += conditionalGroups.Count;

            results.Add(new EnrichedCliOutputTool
            {
                Command = tool.Command,
                Name = tool.Name,
                Description = tool.Description,
                Option = CloneOptions(tool.Option),
                Area = tool.Area,
                AdditionalProperties = CloneExtensionData(tool.AdditionalProperties),
                Enrichment = new ToolEnrichment
                {
                    Matched = matchedCommand is not null,
                    ConditionalGroups = conditionalGroups,
                    ParameterEnhancements = matchedCommand is null
                        ? new Dictionary<string, ParameterEnhancement>(StringComparer.OrdinalIgnoreCase)
                        : _parameterEnricher.Enrich(tool, matchedCommand),
                    Examples = string.IsNullOrWhiteSpace(matchedCommand?.RawBlock)
                        ? null
                        : matchedCommand.RawBlock
                }
            });
        }

        return new EnrichedCliOutputDocument
        {
            Results = results,
            AdditionalProperties = CloneExtensionData(cliOutputDocument.AdditionalProperties),
            EnrichmentMetadata = new EnrichmentMetadata
            {
                TotalTools = cliOutputDocument.Results.Count,
                MatchedTools = matchedTools,
                UnmatchedTools = cliOutputDocument.Results.Count - matchedTools,
                ConditionalGroupsFound = conditionalGroupsFound,
                Timestamp = DateTimeOffset.UtcNow
            }
        };
    }

    private static List<CliOutputOption> CloneOptions(List<CliOutputOption> options)
    {
        return options
            .Select(option => new CliOutputOption
            {
                Name = option.Name,
                AdditionalProperties = CloneExtensionData(option.AdditionalProperties)
            })
            .ToList();
    }

    private static Dictionary<string, JsonElement>? CloneExtensionData(Dictionary<string, JsonElement>? source)
    {
        if (source is null || source.Count == 0)
        {
            return null;
        }

        var clone = new Dictionary<string, JsonElement>(source.Count, StringComparer.Ordinal);
        foreach (var entry in source)
        {
            clone[entry.Key] = entry.Value.Clone();
        }

        return clone;
    }
}
