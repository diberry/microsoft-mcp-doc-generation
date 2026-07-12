using System.Text.Json;
using PipelineRunner.Contracts;
using PipelineRunner.Services;
using PipelineRunner.Steps;

namespace PipelineRunner.Registry;

public sealed class StepRegistry
{
    private readonly IReadOnlyDictionary<int, IPipelineStep> _steps;
    private readonly IReadOnlyDictionary<string, IPipelineStep> _stepsBySlug;

    public StepRegistry(IEnumerable<IPipelineStep> steps)
    {
        var stepArray = steps.ToArray();
        var duplicateIds = stepArray
            .GroupBy(step => step.Id)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(id => id)
            .ToArray();

        if (duplicateIds.Length > 0)
        {
            throw new InvalidOperationException($"Duplicate step identifiers are not allowed: {string.Join(", ", duplicateIds)}.");
        }

        _steps = stepArray.ToDictionary(step => step.Id);
        _stepsBySlug = BuildSlugLookup(stepArray);
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

            var configIds = new HashSet<int>();
            var entryIndex = -1;
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                entryIndex++;

                if (!el.TryGetProperty("id", out var idElement))
                {
                    output.WriteLine($"[WARN] pipeline.config.json entry #{entryIndex} is missing the required 'id' field. Skipping this entry during step registry validation.");
                    continue;
                }

                if (idElement.ValueKind != JsonValueKind.Number || !idElement.TryGetInt32(out var id))
                {
                    output.WriteLine($"[WARN] pipeline.config.json entry #{entryIndex} has a non-integer 'id' value ('{idElement.GetRawText()}'). Skipping this entry during step registry validation.");
                    continue;
                }

                configIds.Add(id);
            }

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

    /// <summary>
    /// Looks up a step by its slug (kebab-case name, type alias, or full step identifier).
    /// Slugs are matched case-insensitively.
    /// </summary>
    public bool TryGetBySlug(string slug, out IPipelineStep? step)
        => _stepsBySlug.TryGetValue(Slugify(slug), out step);

    public IPipelineStep GetStep(int stepId)
        => _steps.TryGetValue(stepId, out var step)
            ? step
            : throw new KeyNotFoundException($"Unknown step id '{stepId}'.");

    /// <summary>
    /// Builds a lookup from slug strings to steps. Each step registers three slug variants:
    /// (1) slugified Name, (2) "step-{id}-{name}" identifier, (3) type-based alias (if unique).
    /// </summary>
    private static IReadOnlyDictionary<string, IPipelineStep> BuildSlugLookup(IEnumerable<IPipelineStep> steps)
    {
        var stepArray = steps.ToArray();
        var lookup = new Dictionary<string, IPipelineStep>(StringComparer.OrdinalIgnoreCase);
        var typeAliasCounts = stepArray
            .Select(step => Slugify(GetTypeAlias(step.GetType().Name)))
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .GroupBy(alias => alias, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (var step in stepArray)
        {
            RegisterSlug(lookup, Slugify(step.Name), step);
            RegisterSlug(lookup, Slugify($"step-{step.Id}-{step.Name}"), step);

            var typeAlias = Slugify(GetTypeAlias(step.GetType().Name));
            if (!string.IsNullOrWhiteSpace(typeAlias) && typeAliasCounts[typeAlias] == 1)
            {
                RegisterSlug(lookup, typeAlias, step);
            }
        }

        return lookup;
    }

    private static void RegisterSlug(IDictionary<string, IPipelineStep> lookup, string slug, IPipelineStep step)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return;
        }

        if (lookup.TryGetValue(slug, out var existing) && existing.Id != step.Id)
        {
            throw new InvalidOperationException($"Duplicate step slug '{slug}' is not allowed.");
        }

        lookup[slug] = step;
    }

    /// <summary>
    /// Converts a PascalCase type name (minus "Step" suffix) to a kebab-case alias.
    /// Handles acronyms correctly: "XMLParser" → "xml-parser", "ToolGeneration" → "tool-generation".
    /// </summary>
    private static string GetTypeAlias(string typeName)
    {
        var alias = typeName.EndsWith("Step", StringComparison.Ordinal)
            ? typeName[..^4]
            : typeName;
        var buffer = new char[alias.Length * 2];
        var length = 0;

        for (var index = 0; index < alias.Length; index++)
        {
            var character = alias[index];
            if (index > 0 && char.IsUpper(character))
            {
                // Insert dash when: uppercase follows lowercase (camelCase boundary)
                // OR uppercase follows uppercase but next char is lowercase (acronym end: "XMLParser" → "XML-Parser")
                var followsLower = char.IsLower(alias[index - 1]);
                var isAcronymEnd = index + 1 < alias.Length && char.IsUpper(alias[index - 1]) && char.IsLower(alias[index + 1]);
                if (followsLower || isAcronymEnd)
                {
                    buffer[length++] = '-';
                }
            }

            buffer[length++] = character;
        }

        return new string(buffer, 0, length);
    }

    /// <summary>
    /// Normalizes a string to a URL-safe kebab-case slug (lowercase, alphanumeric + hyphens).
    /// </summary>
    private static string Slugify(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var buffer = new char[value.Length * 2];
        var length = 0;
        var previousDash = false;

        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer[length++] = character;
                previousDash = false;
                continue;
            }

            if (!previousDash)
            {
                buffer[length++] = '-';
                previousDash = true;
            }
        }

        return new string(buffer, 0, length).Trim('-');
    }
}
