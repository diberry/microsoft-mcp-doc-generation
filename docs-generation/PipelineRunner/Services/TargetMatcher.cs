namespace PipelineRunner.Services;

public sealed class TargetMatcher : ITargetMatcher
{
    public string Normalize(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return target;
        }

        return target.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Trim()
            .Replace('_', ' ');
    }

    public IReadOnlyList<CliTool> FindMatches(IReadOnlyList<CliTool> allTools, string target)
    {
        var normalizedTarget = Normalize(target);
        var matches = allTools
            .Where(tool => string.Equals(tool.Command, normalizedTarget, StringComparison.OrdinalIgnoreCase)
                || tool.Command.StartsWith($"{normalizedTarget} ", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
        {
            throw new InvalidOperationException($"No tools found matching '{normalizedTarget}'.");
        }

        return matches;
    }
}
