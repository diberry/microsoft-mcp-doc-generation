using System.Text.RegularExpressions;
using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Models;

namespace DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Services;

public sealed class ConditionalParamExtractor
{
    private static readonly Regex ParameterRegex = new(
        @"--[A-Za-z0-9-]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyList<ConditionalPattern> Patterns =
    [
        new(
            "requires_at_least_one",
            new Regex(@"Requires at least one[^.]*\.?", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            ExtractParametersFromMatch),

        new(
            "list_or_get",
            new Regex(@"(?:list all .+? or (?:get|retrieve)|get .+? or list|list .+? or (?:get|retrieve) .+?(?:by|if))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            ExtractListOrGetParameters),

        new(
            "create_or_update",
            new Regex(@"(?:create or (?:update|set)|(?:already exists).+?(?:creates? a new|update))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            null),

        new(
            "either_or_param",
            new Regex(@"(?:(?:accepts?|provide|specify) (?:either|both) .+? or .+?(?:--[a-z-]+))", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant),
            ExtractParametersFromFullDescription),
    ];

    public List<ConditionalParameterGroup> Extract(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return [];
        }

        var groups = new List<ConditionalParameterGroup>();

        foreach (var pattern in Patterns)
        {
            var matches = pattern.Regex.Matches(description);
            if (matches.Count == 0)
            {
                continue;
            }

            foreach (Match match in matches)
            {
                var parameters = pattern.ParameterExtractor is not null
                    ? pattern.ParameterExtractor(match, description)
                    : [];

                // requires_at_least_one must have at least one --param to be meaningful
                if (pattern.Type == "requires_at_least_one" && parameters.Count == 0)
                {
                    continue;
                }

                groups.Add(new ConditionalParameterGroup
                {
                    Type = pattern.Type,
                    Parameters = parameters,
                    Source = "description_regex",
                    Description = match.Value.Trim().TrimEnd('.')
                });

                // For non-repeatable patterns, only take the first match
                if (pattern.Type != "requires_at_least_one")
                {
                    break;
                }
            }
        }

        return groups;
    }

    private static List<string> ExtractParametersFromMatch(Match match, string _)
    {
        return ParameterRegex.Matches(match.Value)
            .Select(parameterMatch => parameterMatch.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ExtractParametersFromFullDescription(Match _, string description)
    {
        return ParameterRegex.Matches(description)
            .Select(parameterMatch => parameterMatch.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ExtractListOrGetParameters(Match match, string description)
    {
        // For list_or_get, find the optional parameter that switches between modes
        // Pattern: "If --name is provided" or "by --name" or similar
        var switchParamRegex = new Regex(
            @"(?:if\s+)(--[a-z-]+)\s+is\s+provided|(?:by\s+)(--[a-z-]+)|(?:specify\s+)(--[a-z-]+)",
            RegexOptions.IgnoreCase);

        var switchMatch = switchParamRegex.Match(description);
        if (switchMatch.Success)
        {
            var param = switchMatch.Groups.Cast<Group>()
                .Skip(1)
                .FirstOrDefault(group => group.Success)?.Value;
            if (param is not null)
            {
                return [param];
            }
        }

        return [];
    }

    private sealed record ConditionalPattern(
        string Type,
        Regex Regex,
        Func<Match, string, List<string>>? ParameterExtractor);
}
