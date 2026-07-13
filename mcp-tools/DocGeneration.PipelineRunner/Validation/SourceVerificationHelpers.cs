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
