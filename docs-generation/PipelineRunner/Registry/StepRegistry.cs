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
            new ShimStep(1, "Generate annotations, parameters, and raw tools", FailurePolicy.Fatal, Path.Combine(scriptsRoot, "1-Generate-AnnotationsParametersRaw-One.ps1"), "ToolCommand", expectedOutputs: ["annotations", "parameters", "tools-raw"]),
            new ShimStep(2, "Generate example prompts", FailurePolicy.Fatal, Path.Combine(scriptsRoot, "2-Generate-ExamplePrompts-One.ps1"), "ToolCommand", dependsOn: [1], requiresAiConfiguration: true, expectedOutputs: ["example-prompts", "example-prompts-prompts", "example-prompts-raw-output"]),
            new ShimStep(3, "Compose and improve tool files", FailurePolicy.Fatal, Path.Combine(scriptsRoot, "3-Generate-ToolGenerationAndAIImprovements-One.ps1"), "ToolCommand", dependsOn: [1, 2], requiresAiConfiguration: true, expectedOutputs: ["tools-composed", "tools"]),
            new ShimStep(4, "Generate tool-family article", FailurePolicy.Fatal, Path.Combine(scriptsRoot, "4-Generate-ToolFamilyCleanup-One.ps1"), "ToolCommand", dependsOn: [3], requiresAiConfiguration: true, usesIsolatedWorkspace: true, expectedOutputs: ["tool-family-metadata", "tool-family-related", "tool-family", "reports"]),
            new ShimStep(5, "Generate skills relevance", FailurePolicy.Warn, Path.Combine(scriptsRoot, "5-Generate-SkillsRelevance-One.ps1"), "ServiceArea", expectedOutputs: ["skills-relevance"]),
            new ShimStep(6, "Generate horizontal article", FailurePolicy.Fatal, Path.Combine(scriptsRoot, "6-Generate-HorizontalArticles-One.ps1"), "ServiceArea", requiresAiConfiguration: true, expectedOutputs: ["horizontal-articles"]),
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
