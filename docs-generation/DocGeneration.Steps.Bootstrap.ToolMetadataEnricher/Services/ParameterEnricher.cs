using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Models;

namespace DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Services;

public sealed class ParameterEnricher
{
    private readonly Dictionary<string, AzmcpGlobalOption> _globalOptionsByName;

    public ParameterEnricher(IEnumerable<AzmcpGlobalOption> globalOptions)
    {
        ArgumentNullException.ThrowIfNull(globalOptions);

        _globalOptionsByName = globalOptions
            .Where(option => !string.IsNullOrWhiteSpace(option.Name))
            .GroupBy(option => NormalizeParameterName(option.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
    }

    public Dictionary<string, ParameterEnhancement> Enrich(CliOutputTool cliTool, AzmcpCommand azmcpCommand)
    {
        ArgumentNullException.ThrowIfNull(cliTool);
        ArgumentNullException.ThrowIfNull(azmcpCommand);

        var commandParameters = azmcpCommand.Parameters
            .Where(parameter => !string.IsNullOrWhiteSpace(parameter.Name))
            .GroupBy(parameter => NormalizeParameterName(parameter.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

        var enhancements = new Dictionary<string, ParameterEnhancement>(StringComparer.OrdinalIgnoreCase);

        foreach (var option in cliTool.Option)
        {
            if (string.IsNullOrWhiteSpace(option.Name))
            {
                continue;
            }

            var normalizedName = NormalizeParameterName(option.Name);
            commandParameters.TryGetValue(normalizedName, out var commandParameter);
            _globalOptionsByName.TryGetValue(normalizedName, out var globalOption);

            var enhancement = CreateEnhancement(commandParameter, globalOption);
            if (enhancement is null)
            {
                continue;
            }

            enhancements[option.Name] = enhancement;
        }

        return enhancements;
    }

    internal static string NormalizeParameterName(string? parameterName)
    {
        return string.IsNullOrWhiteSpace(parameterName)
            ? string.Empty
            : parameterName.Trim().ToLowerInvariant();
    }

    private static ParameterEnhancement? CreateEnhancement(
        AzmcpCommandParameter? commandParameter,
        AzmcpGlobalOption? globalOption)
    {
        var defaultValue = NormalizeDefaultValue(commandParameter?.Default)
            ?? NormalizeDefaultValue(globalOption?.Default);

        var valuePlaceholder = FirstNonEmpty(commandParameter?.ValuePlaceholder, globalOption?.ValuePlaceholder);

        var allowedValues = commandParameter?.AllowedValues?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allowedValues is { Count: 0 })
        {
            allowedValues = null;
        }

        if (defaultValue is null && valuePlaceholder is null && allowedValues is null)
        {
            return null;
        }

        return new ParameterEnhancement
        {
            DefaultValue = defaultValue,
            ValuePlaceholder = valuePlaceholder,
            AllowedValues = allowedValues
        };
    }

    private static string? FirstNonEmpty(params string?[] candidates)
    {
        return candidates.FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate))?.Trim();
    }

    private static string? NormalizeDefaultValue(string? defaultValue)
    {
        if (string.IsNullOrWhiteSpace(defaultValue))
        {
            return null;
        }

        var normalized = defaultValue.Trim();
        if (normalized == "-")
        {
            return null;
        }

        normalized = normalized.Replace("`", string.Empty, StringComparison.Ordinal);

        if (normalized.Length > 1 && normalized.StartsWith('\'') && normalized.EndsWith('\''))
        {
            normalized = normalized[1..^1];
        }

        const string environmentVariablePrefix = "Environment variable ";
        if (normalized.StartsWith(environmentVariablePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var variableName = normalized[environmentVariablePrefix.Length..].Trim();
            if (variableName.Length > 0)
            {
                return $"{variableName} environment variable";
            }
        }

        return normalized;
    }
}
