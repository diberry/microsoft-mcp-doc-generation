using System.Text.Json;
using System.Text.RegularExpressions;
using Shared;

namespace PipelineRunner.Validation;

internal static class SourceVerificationHelpers
{
    public static string NormalizeToolCommand(string command)
        => string.IsNullOrWhiteSpace(command)
            ? command
            : command.Replace("\r", string.Empty, StringComparison.Ordinal).Trim().Replace('_', ' ');

    public static string NormalizeParameterName(string parameterName)
    {
        var normalized = ParameterCoverageChecker.RemoveMarkup(parameterName)
            .Trim()
            .TrimStart('-')
            .ToLowerInvariant();
        normalized = Regex.Replace(normalized, "[\\s_]+", "-");
        normalized = Regex.Replace(normalized, "[^a-z0-9\\-]+", "-");
        return normalized.Trim('-');
    }

    public static IReadOnlyDictionary<string, string> LoadNaturalLanguageReverseMap(string? mcpToolsRoot)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(mcpToolsRoot))
        {
            return map;
        }

        var path = Path.Combine(mcpToolsRoot, "data", "nl-parameter-identifiers.json");
        if (!File.Exists(path))
        {
            return map;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return map;
            }

            foreach (var entry in document.RootElement.EnumerateArray())
            {
                if (!entry.TryGetProperty("Parameter", out var parameterProperty)
                    || parameterProperty.ValueKind != JsonValueKind.String
                    || !entry.TryGetProperty("NaturalLanguage", out var naturalLanguageProperty)
                    || naturalLanguageProperty.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var cliName = NormalizeParameterName(parameterProperty.GetString() ?? string.Empty);
                var displaySlug = NormalizeParameterName(naturalLanguageProperty.GetString() ?? string.Empty);
                if (string.IsNullOrWhiteSpace(cliName) || string.IsNullOrWhiteSpace(displaySlug))
                {
                    continue;
                }

                map[displaySlug] = cliName;
            }
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return map;
    }

    // Reverse-map a documented display slug (e.g. "resource-name") back to its canonical CLI name
    // (e.g. "resource") only when that CLI name is actually present in the tool's source parameters.
    // The per-tool guard prevents mis-rewriting a raw CLI option whose name happens to collide with a
    // display slug: the rewrite applies only if it resolves to a real source parameter.
    public static string ResolveDocumentedParameterName(
        string normalizedName,
        IReadOnlyDictionary<string, string> reverseMap,
        ISet<string> sourceParameterNames)
        => reverseMap.TryGetValue(normalizedName, out var cliName) && sourceParameterNames.Contains(cliName)
            ? cliName
            : normalizedName;

    public static IReadOnlyList<SourceParameter> GetSourceParameters(JsonElement options)
    {
        var parameters = new List<SourceParameter>();
        foreach (var option in options.EnumerateArray())
        {
            if (!option.TryGetProperty("name", out var nameProperty) || nameProperty.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var normalizedName = NormalizeParameterName(nameProperty.GetString() ?? string.Empty);
            if (string.IsNullOrWhiteSpace(normalizedName))
            {
                continue;
            }

            parameters.Add(new SourceParameter(normalizedName, IsSourceParameterRequired(option)));
        }

        return parameters;
    }

    private static bool IsSourceParameterRequired(JsonElement option)
    {
        if (!option.TryGetProperty("required", out var requiredProperty))
        {
            return false;
        }

        return requiredProperty.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => Regex.IsMatch(requiredProperty.GetString() ?? string.Empty, "(?i)^(true|yes|required)$"),
            _ => false,
        };
    }
}

internal sealed record SourceParameter(string NormalizedName, bool Required);
