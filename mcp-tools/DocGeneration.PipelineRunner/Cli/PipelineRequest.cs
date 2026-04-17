namespace PipelineRunner.Cli;

public sealed record PipelineRequest(
    string? Namespace,
    IReadOnlyList<int> Steps,
    string OutputPath,
    bool SkipBuild,
    bool SkipValidation,
    bool DryRun,
    bool SkipEnvValidation = false,
    bool SkipDependencyValidation = false,
    string? McpBranch = null)
{
    /// <summary>
    /// Default upstream branch for fetching files from the microsoft/mcp repository.
    /// </summary>
    public const string DefaultMcpBranch = "release/azure/2.x";

    /// <summary>
    /// Resolves the effective MCP branch: CLI flag > MCP_BRANCH env var > default constant.
    /// Blank values are treated as unset.
    /// </summary>
    public string ResolvedMcpBranch
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(McpBranch))
                return McpBranch.Trim();

            var envValue = Environment.GetEnvironmentVariable("MCP_BRANCH");
            if (!string.IsNullOrWhiteSpace(envValue))
                return envValue.Trim();

            return DefaultMcpBranch;
        }
    }
    public static IReadOnlyList<int> DefaultSteps { get; } = [1, 2, 3, 4, 5, 6];

    public static string GetDefaultOutputPath(string? targetNamespace)
        => string.IsNullOrWhiteSpace(targetNamespace)
            ? ".\\generated"
            : $".\\generated-{targetNamespace.Trim()}";

    public static bool TryParseSteps(string? csv, out IReadOnlyList<int> steps, out string? error)
    {
        steps = Array.Empty<int>();
        error = null;

        if (string.IsNullOrWhiteSpace(csv))
        {
            error = "At least one step must be supplied.";
            return false;
        }

        var parsed = new List<int>();
        foreach (var part in csv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(part, out var stepId))
            {
                error = $"Invalid step identifier '{part}'.";
                return false;
            }

            parsed.Add(stepId);
        }

        if (parsed.Count == 0)
        {
            error = "At least one step must be supplied.";
            return false;
        }

        steps = parsed;
        return true;
    }

    public IReadOnlyList<string> Validate(IReadOnlyCollection<int>? validStepIds = null)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(OutputPath))
        {
            errors.Add("OutputPath is required.");
        }

        if (Steps.Count == 0)
        {
            errors.Add("At least one step must be selected.");
        }

        var duplicates = Steps
            .GroupBy(step => step)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(value => value)
            .ToArray();

        if (duplicates.Length > 0)
        {
            errors.Add($"Duplicate step identifiers are not allowed: {string.Join(", ", duplicates)}.");
        }

        var allowedSteps = validStepIds ?? DefaultSteps;
        var invalidSteps = Steps
            .Where(step => !allowedSteps.Contains(step))
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        if (invalidSteps.Length > 0)
        {
            errors.Add($"Unsupported step identifiers: {string.Join(", ", invalidSteps)}.");
        }

        if (Namespace is not null && string.IsNullOrWhiteSpace(Namespace))
        {
            errors.Add("Namespace cannot be blank when supplied.");
        }

        return errors;
    }
}
