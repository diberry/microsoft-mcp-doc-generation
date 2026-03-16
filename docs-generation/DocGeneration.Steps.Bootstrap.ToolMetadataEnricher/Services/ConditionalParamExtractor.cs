using System.Text.RegularExpressions;
using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Models;

namespace DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Services;

public sealed class ConditionalParamExtractor
{
    private static readonly Regex RequirementRegex = new(
        @"Requires at least one[^.]*\.?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ParameterRegex = new(
        @"--[A-Za-z0-9-]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public List<ConditionalParameterGroup> Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return [];
        }

        var groups = new List<ConditionalParameterGroup>();

        foreach (Match match in RequirementRegex.Matches(description))
        {
            var parameters = ParameterRegex.Matches(match.Value)
                .Select(parameterMatch => parameterMatch.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (parameters.Count == 0)
            {
                continue;
            }

            groups.Add(new ConditionalParameterGroup
            {
                Type = "requires_at_least_one",
                Parameters = parameters,
                Source = "description_regex"
            });
        }

        return groups;
    }
}
