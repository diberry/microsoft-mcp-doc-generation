using System.Text.RegularExpressions;
using DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Models;

namespace DocGeneration.Steps.Bootstrap.ToolMetadataEnricher.Services;

public sealed class ToolMatcher
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private readonly Dictionary<string, AzmcpCommand> _commandsByNormalizedName;

    public ToolMatcher(AzmcpCommandsDocument azmcpCommandsDocument)
    {
        ArgumentNullException.ThrowIfNull(azmcpCommandsDocument);

        _commandsByNormalizedName = BuildLookup(azmcpCommandsDocument);
    }

    public AzmcpCommand? Match(CliOutputTool cliTool)
    {
        ArgumentNullException.ThrowIfNull(cliTool);
        return Match(cliTool.Command);
    }

    public AzmcpCommand? Match(string commandText)
    {
        var normalizedCommand = NormalizeCommand(commandText);
        return _commandsByNormalizedName.GetValueOrDefault(normalizedCommand);
    }

    internal static string NormalizeCommand(string? commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
        {
            return string.Empty;
        }

        var normalized = commandText.Trim();
        if (normalized.StartsWith("azmcp ", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["azmcp ".Length..];
        }

        normalized = WhitespaceRegex.Replace(normalized, " ");
        return normalized.Trim().ToLowerInvariant();
    }

    private static Dictionary<string, AzmcpCommand> BuildLookup(AzmcpCommandsDocument azmcpCommandsDocument)
    {
        var lookup = new Dictionary<string, AzmcpCommand>(StringComparer.OrdinalIgnoreCase);

        foreach (var command in EnumerateCommands(azmcpCommandsDocument))
        {
            if (command.IsExample || string.IsNullOrWhiteSpace(command.CommandText))
            {
                continue;
            }

            var normalizedCommand = NormalizeCommand(command.CommandText);
            if (normalizedCommand.Length == 0 || lookup.ContainsKey(normalizedCommand))
            {
                continue;
            }

            lookup[normalizedCommand] = command;
        }

        return lookup;
    }

    private static IEnumerable<AzmcpCommand> EnumerateCommands(AzmcpCommandsDocument azmcpCommandsDocument)
    {
        foreach (var serviceSection in azmcpCommandsDocument.ServiceSections)
        {
            foreach (var command in serviceSection.Commands)
            {
                yield return command;
            }

            foreach (var subSection in serviceSection.SubSections)
            {
                foreach (var command in subSection.Commands)
                {
                    yield return command;
                }
            }
        }
    }
}
