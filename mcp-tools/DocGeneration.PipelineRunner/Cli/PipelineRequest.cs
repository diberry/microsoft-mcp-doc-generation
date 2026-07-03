namespace PipelineRunner.Cli;

/// <summary>
/// Validated, immutable request object produced by <see cref="PipelineCli"/> and consumed by
/// <see cref="PipelineRunner"/>. Carries all resolved options for a single pipeline invocation,
/// including replay mode (<see cref="Replay"/>) and inspect mode (<see cref="Inspect"/>) flags.
/// </summary>
public sealed record PipelineRequest(
    string? Namespace,
    IReadOnlyList<int> Steps,
    string OutputPath,
    bool SkipBuild,
    bool SkipValidation,
    bool DryRun,
    bool SkipEnvValidation = false,
    bool SkipDependencyValidation = false,
    string? McpBranch = null,
    bool SkipChangelogGate = false,
    bool RunFingerprintGate = false,
    bool RunPromptRegressionGate = false,
    bool SkipNpmUpdate = false,
    bool Replay = false,
    string? ReplayFromRunId = null,
    string? ReplayStepName = null,
    bool Inspect = false,
    string? InspectShow = null,
    bool WriteJsonOutput = false)
{
    /// <summary>
    /// Default upstream branch for fetching files from the microsoft/mcp repository.
    /// </summary>
    public const string DefaultMcpBranch = "main";

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

    /// <summary>
    /// Parse-time validation allowlist for explicit <c>--steps</c> requests.
    /// This includes Bootstrap (step 0) even though <see cref="DefaultSteps"/> omits it from the default run set.
    /// Keep this list aligned with <see cref="Registry.StepRegistry.CreateDefault(string)"/>.
    /// </summary>
    public static IReadOnlyList<int> AllValidSteps { get; } = [0, 1, 2, 3, 4, 5, 6, 7, 8];

    /// <summary>
    /// Default namespace step run set used when <c>--steps</c> is omitted.
    /// Bootstrap (step 0) is not included because it is added automatically by the runner.
    /// </summary>
    public static IReadOnlyList<int> DefaultSteps { get; } = [1, 2, 3, 4, 5, 6, 7, 8];

    public static string GetDefaultOutputPath(string? targetNamespace, TimeProvider? timeProvider = null)
    {
        // Human-readable timestamp format (yyyy-MM-dd-HH-mm-ss) keeps default paths easy to sort and identify.
        // Hyphens separate all components for improved readability while maintaining sort stability.
        // Callers can still pass --output explicitly when they need a fully caller-controlled path.
        var timestamp = (timeProvider ?? TimeProvider.System).GetUtcNow().ToString("yyyy-MM-dd-HH-mm-ss");
        return string.IsNullOrWhiteSpace(targetNamespace)
            ? $".\\generated-{timestamp}"
            : $".\\generated-{targetNamespace.Trim()}-{timestamp}";
    }

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

        if (Replay)
        {
            if (string.IsNullOrWhiteSpace(ReplayFromRunId))
            {
                errors.Add("--from is required when --replay is set.");
            }

            if (string.IsNullOrWhiteSpace(ReplayStepName))
            {
                errors.Add("--step-name is required when --replay is set.");
            }
        }
        else if (Inspect)
        {
            if (string.IsNullOrWhiteSpace(ReplayStepName))
            {
                errors.Add("--step-name is required when --inspect is set.");
            }
        }
        else if (Steps.Count == 0)
        {
            errors.Add("At least one step must be selected.");
        }

        var duplicates = Replay || Inspect
            ? Array.Empty<int>()
            : Steps
                .GroupBy(step => step)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(value => value)
                .ToArray();

        if (duplicates.Length > 0)
        {
            errors.Add($"Duplicate step identifiers are not allowed: {string.Join(", ", duplicates)}.");
        }

        var allowedSteps = validStepIds ?? AllValidSteps;
        var invalidSteps = Replay || Inspect
            ? Array.Empty<int>()
            : Steps
                .Where(step => !allowedSteps.Contains(step))
                .Distinct()
                .OrderBy(value => value)
                .ToArray();

        if (invalidSteps.Length > 0)
        {
            var orderedAllowedSteps = allowedSteps.OrderBy(value => value).ToArray();
            var validStepDescription = orderedAllowedSteps.Length == 0
                ? "(none)"
                : orderedAllowedSteps.SequenceEqual(Enumerable.Range(orderedAllowedSteps.First(), orderedAllowedSteps.Length))
                    ? $"{orderedAllowedSteps.First()}-{orderedAllowedSteps.Last()}"
                    : string.Join(", ", orderedAllowedSteps);
            errors.Add($"Unsupported step identifiers: {string.Join(", ", invalidSteps)}. Valid step identifiers: {validStepDescription}.");
        }

        if (Namespace is not null && string.IsNullOrWhiteSpace(Namespace))
        {
            errors.Add("Namespace cannot be blank when supplied.");
        }

        return errors;
    }
}
