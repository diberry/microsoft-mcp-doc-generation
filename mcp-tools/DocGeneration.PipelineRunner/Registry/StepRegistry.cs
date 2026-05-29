using System.Text.Json;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Steps;

namespace PipelineRunner.Registry;

public sealed class StepRegistry
{
    private readonly IReadOnlyDictionary<int, IPipelineStep> _steps;

    public StepRegistry(IEnumerable<IPipelineStep> steps)
    {
        var duplicateIds = steps
            .GroupBy(step => step.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id)
            .ToArray();

        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate step identifiers are not allowed: {string.Join(", ", duplicateIds)}.");
        }

        _steps = steps.ToDictionary(step => step.Id);
    }

    public static StepRegistry CreateDefault(string scriptsRoot)
    {
        var registry = new StepRegistry([
            new BootstrapStep(new NamespaceMappingEmitter()),
            new AnnotationsParametersRawStep(),
            new ExamplePromptsStep(),
            new ToolGenerationStep(),
            new ToolFamilyCleanupStep(),
            new SkillsRelevanceStep(),
            new HorizontalArticlesStep(),
            new ArticleHealthValidatorStep(),
            new CoverageAuditStep(),
        ]);

        var configPath = Path.GetFullPath(Path.Combine(scriptsRoot, "..", "pipeline.config.json"));
        ValidateAgainstConfig(registry, configPath);

        return registry;
    }

    /// <summary>
    /// Compares the in-memory step registry against <c>pipeline.config.json</c> and emits
    /// warnings for any divergence.  Phase 1 behavior: warning only — the pipeline continues.
    /// </summary>
    /// <param name="registry">The populated registry to validate.</param>
    /// <param name="configPath">Absolute path to <c>pipeline.config.json</c>.</param>
    /// <param name="warningOutput">
    ///   Writer to receive warning messages.  Defaults to <see cref="Console.Error"/>.
    ///   Supply a <see cref="StringWriter"/> in tests to capture output without side-effects.
    /// </param>
    internal static void ValidateAgainstConfig(StepRegistry registry, string configPath, TextWriter? warningOutput = null)
    {
        var output = warningOutput ?? Console.Error;

        if (!File.Exists(configPath))
        {
            output.WriteLine($"[WARN] pipeline.config.json not found at '{configPath}'. Step registry validation skipped.");
            return;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            using var doc = JsonDocument.Parse(json);

            var configIds = doc.RootElement
                .EnumerateArray()
                .Select(el => el.GetProperty("id").GetInt32())
                .ToHashSet();

            var registryIds = registry.GetAllSteps()
                .Select(step => step.Id)
                .ToHashSet();

            var onlyInConfig = configIds.Except(registryIds).OrderBy(id => id).ToArray();
            var onlyInRegistry = registryIds.Except(configIds).OrderBy(id => id).ToArray();

            if (onlyInConfig.Length > 0)
            {
                output.WriteLine($"[WARN] StepRegistry divergence: step IDs in pipeline.config.json but not in registry: {string.Join(", ", onlyInConfig)}");
                // Phase 2+: throw StepRegistryConfigMismatchException here
            }

            if (onlyInRegistry.Length > 0)
            {
                output.WriteLine($"[WARN] StepRegistry divergence: step IDs in registry but not in pipeline.config.json: {string.Join(", ", onlyInRegistry)}");
                // Phase 2+: throw StepRegistryConfigMismatchException here
            }
        }
        catch (JsonException ex)
        {
            output.WriteLine($"[WARN] Failed to parse pipeline.config.json: {ex.Message}. Step registry validation skipped.");
        }
    }

    public IReadOnlyList<IPipelineStep> GetAllSteps()
        => _steps.Values.OrderBy(step => step.Id).ToArray();

    public IReadOnlyList<IPipelineStep> GetOrderedSteps(IEnumerable<int> stepIds)
        => GetAllSteps()
            .Where(step => step.Scope == StepScope.Global)
            .Concat(stepIds.Select(GetStep))
            .GroupBy(step => step.Id)
            .Select(group => group.First())
            .OrderBy(step => step.Id)
            .ToArray();

    public IPipelineStep GetStep(int stepId)
        => _steps.TryGetValue(stepId, out var step)
            ? step
            : throw new KeyNotFoundException($"Unknown step id '{stepId}'.");
}
