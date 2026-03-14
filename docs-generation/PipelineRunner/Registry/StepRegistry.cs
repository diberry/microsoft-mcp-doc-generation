using PipelineRunner.Contracts;
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
        => new([
            new AnnotationsParametersRawStep(),
            new ExamplePromptsStep(),
            new ToolGenerationStep(),
            new ShimStep(4, "Generate tool-family article", FailurePolicy.Fatal, Path.Combine(scriptsRoot, "4-Generate-ToolFamilyCleanup-One.ps1"), "ToolCommand", dependsOn: [3], requiresAiConfiguration: true, usesIsolatedWorkspace: true, expectedOutputs: ["tool-family-metadata", "tool-family-related", "tool-family", "reports"]),
            new ShimStep(5, "Generate skills relevance", FailurePolicy.Warn, Path.Combine(scriptsRoot, "5-Generate-SkillsRelevance-One.ps1"), "ServiceArea", expectedOutputs: ["skills-relevance"]),
            new HorizontalArticlesStep(),
        ]);

    public IReadOnlyList<IPipelineStep> GetAllSteps()
        => _steps.Values.OrderBy(step => step.Id).ToArray();

    public IReadOnlyList<IPipelineStep> GetOrderedSteps(IEnumerable<int> stepIds)
        => stepIds.Select(GetStep).OrderBy(step => step.Id).ToArray();

    public IPipelineStep GetStep(int stepId)
        => _steps.TryGetValue(stepId, out var step)
            ? step
            : throw new KeyNotFoundException($"Unknown step id '{stepId}'.");
}
