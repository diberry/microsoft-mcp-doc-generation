using PipelineRunner.Contracts;
using PipelineRunner.Registry;
using PipelineRunner.Steps;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Contract tests that verify each pipeline step's declared I/O metadata
/// (DependsOn, ExpectedOutputs, Scope, FailurePolicy, flags) without executing
/// any step logic. These tests guard against silent dependency drift and ensure
/// the step chain remains consistent.
/// </summary>
public class StepContractTests
{
    // ── Step identity & ordering ──────────────────────────────────────

    [Fact]
    public void AllSteps_HaveUniqueIds()
    {
        var steps = CreateAllSteps();
        var ids = steps.Select(s => s.Id).ToArray();

        Assert.Equal(ids.Distinct(), ids);
    }

    [Fact]
    public void AllSteps_AreRegisteredInAscendingIdOrder()
    {
        var steps = CreateAllSteps();
        var ids = steps.Select(s => s.Id).ToArray();

        Assert.Equal(ids.OrderBy(id => id), ids);
    }

    [Fact]
    public void DefaultRegistry_ContainsExactlySevenSteps()
    {
        var registry = CreateDefaultRegistry();
        var steps = registry.GetAllSteps();

        Assert.Equal(7, steps.Count);
    }

    // ── Bootstrap (Step 0) ────────────────────────────────────────────

    [Fact]
    public void BootstrapStep_IsGlobalScope()
    {
        var step = new BootstrapStep();

        Assert.Equal(StepScope.Global, step.Scope);
    }

    [Fact]
    public void BootstrapStep_HasNoDependencies()
    {
        var step = new BootstrapStep();

        Assert.Empty(step.DependsOn);
    }

    [Fact]
    public void BootstrapStep_HasFatalFailurePolicy()
    {
        var step = new BootstrapStep();

        Assert.Equal(FailurePolicy.Fatal, step.FailurePolicy);
    }

    [Fact]
    public void BootstrapStep_DeclaresExpectedOutputDirectories()
    {
        var step = new BootstrapStep();

        Assert.Contains("cli", step.ExpectedOutputs);
        Assert.Contains("e2e-test-prompts", step.ExpectedOutputs);
        Assert.Contains("logs", step.ExpectedOutputs);
        Assert.Contains("tools", step.ExpectedOutputs);
        Assert.Contains("example-prompts", step.ExpectedOutputs);
        Assert.Contains("annotations", step.ExpectedOutputs);
        Assert.Contains("tool-family", step.ExpectedOutputs);
    }

    [Fact]
    public void BootstrapStep_DoesNotRequireCliOutput()
    {
        var step = new BootstrapStep();
        var definition = (StepDefinition)step;

        Assert.False(definition.RequiresCliOutput);
        Assert.False(definition.RequiresCliVersion);
    }

    // ── Step 1: AnnotationsParametersRaw ──────────────────────────────

    [Fact]
    public void Step1_HasNoDeclaredDependencies()
    {
        var step = new AnnotationsParametersRawStep();

        Assert.Empty(step.DependsOn);
    }

    [Fact]
    public void Step1_IsNamespaceScope()
    {
        var step = new AnnotationsParametersRawStep();

        Assert.Equal(StepScope.Namespace, step.Scope);
    }

    [Fact]
    public void Step1_DeclaresAnnotationsParametersAndRawToolsOutputs()
    {
        var step = new AnnotationsParametersRawStep();

        Assert.Contains("annotations", step.ExpectedOutputs);
        Assert.Contains("parameters", step.ExpectedOutputs);
        Assert.Contains("tools-raw", step.ExpectedOutputs);
    }

    [Fact]
    public void Step1_HasFatalFailurePolicy()
    {
        var step = new AnnotationsParametersRawStep();

        Assert.Equal(FailurePolicy.Fatal, step.FailurePolicy);
    }

    // ── Step 2: ExamplePrompts (depends on Step 1) ────────────────────

    [Fact]
    public void Step2_DependsOnStep1()
    {
        var step = new ExamplePromptsStep();

        Assert.Equal(new[] { 1 }, step.DependsOn);
    }

    [Fact]
    public void Step2_RequiresAiConfiguration()
    {
        var step = new ExamplePromptsStep();
        var definition = (StepDefinition)step;

        Assert.True(definition.RequiresAiConfiguration);
    }

    [Fact]
    public void Step2_DeclaresExamplePromptOutputDirectories()
    {
        var step = new ExamplePromptsStep();

        Assert.Contains("example-prompts", step.ExpectedOutputs);
        Assert.Contains("example-prompts-prompts", step.ExpectedOutputs);
        Assert.Contains("example-prompts-raw-output", step.ExpectedOutputs);
    }

    // ── Step 3: ToolGeneration (depends on Steps 1 and 2) ─────────────

    [Fact]
    public void Step3_DependsOnSteps1And2()
    {
        var step = new ToolGenerationStep();

        Assert.Equal(new[] { 1, 2 }, step.DependsOn);
    }

    [Fact]
    public void Step3_RequiresAiConfiguration()
    {
        var step = new ToolGenerationStep();
        var definition = (StepDefinition)step;

        Assert.True(definition.RequiresAiConfiguration);
    }

    [Fact]
    public void Step3_DeclaresComposedAndFinalToolOutputs()
    {
        var step = new ToolGenerationStep();

        Assert.Contains("tools-composed", step.ExpectedOutputs);
        Assert.Contains("tools", step.ExpectedOutputs);
    }

    [Fact]
    public void Step3_ConsumesStep1Outputs_AnnotationsAndParameters()
    {
        var step1 = new AnnotationsParametersRawStep();
        var step3 = new ToolGenerationStep();

        // Step 3 depends on Step 1's outputs (annotations, parameters, tools-raw)
        Assert.Contains(1, step3.DependsOn);
        // Step 1 declares these as expected outputs
        Assert.Contains("annotations", step1.ExpectedOutputs);
        Assert.Contains("parameters", step1.ExpectedOutputs);
        Assert.Contains("tools-raw", step1.ExpectedOutputs);
    }

    [Fact]
    public void Step3_ConsumesStep2Outputs_ExamplePrompts()
    {
        var step2 = new ExamplePromptsStep();
        var step3 = new ToolGenerationStep();

        Assert.Contains(2, step3.DependsOn);
        Assert.Contains("example-prompts", step2.ExpectedOutputs);
    }

    // ── Step 4: ToolFamilyCleanup (depends on Step 3) ─────────────────

    [Fact]
    public void Step4_DependsOnStep3()
    {
        var step = new ToolFamilyCleanupStep();

        Assert.Equal(new[] { 3 }, step.DependsOn);
    }

    [Fact]
    public void Step4_ConsumesStep3Output_Tools()
    {
        var step3 = new ToolGenerationStep();
        var step4 = new ToolFamilyCleanupStep();

        Assert.Contains(3, step4.DependsOn);
        Assert.Contains("tools", step3.ExpectedOutputs);
    }

    [Fact]
    public void Step4_DeclaresToolFamilyOutputDirectories()
    {
        var step = new ToolFamilyCleanupStep();

        Assert.Contains("tool-family-metadata", step.ExpectedOutputs);
        Assert.Contains("tool-family-related", step.ExpectedOutputs);
        Assert.Contains("tool-family", step.ExpectedOutputs);
        Assert.Contains("reports", step.ExpectedOutputs);
    }

    [Fact]
    public void Step4_RequiresAiAndUsesIsolatedWorkspace()
    {
        var step = new ToolFamilyCleanupStep();
        var definition = (StepDefinition)step;

        Assert.True(definition.RequiresAiConfiguration);
        Assert.True(definition.UsesIsolatedWorkspace);
    }

    [Fact]
    public void Step4_HasRetryPolicy()
    {
        var step = new ToolFamilyCleanupStep();

        Assert.Equal(2, step.MaxRetries);
    }

    // ── Step 5: SkillsRelevance (no declared deps — AD-020) ───────────

    [Fact]
    public void Step5_HasNoExplicitDependencies_ImplicitBootstrapDependency()
    {
        // AD-020: Step 5 has NO declared DependsOn but implicitly requires
        // Bootstrap metadata (CliOutput, CliVersion via NamespaceStepBase.ResolveTarget).
        // This test documents the gap — if Step 5 gains explicit dependencies, update this.
        var step = new SkillsRelevanceStep();

        Assert.Empty(step.DependsOn);
    }

    [Fact]
    public void Step5_HasWarnFailurePolicy()
    {
        var step = new SkillsRelevanceStep();

        Assert.Equal(FailurePolicy.Warn, step.FailurePolicy);
    }

    [Fact]
    public void Step5_DeclaresSkillsRelevanceOutput()
    {
        var step = new SkillsRelevanceStep();

        Assert.Contains("skills-relevance", step.ExpectedOutputs);
    }

    [Fact]
    public void Step5_DoesNotRequireAiConfiguration()
    {
        var step = new SkillsRelevanceStep();
        var definition = (StepDefinition)step;

        Assert.False(definition.RequiresAiConfiguration);
    }

    // ── Step 6: HorizontalArticles (no declared deps — AD-020) ────────

    [Fact]
    public void Step6_HasNoExplicitDependencies_ImplicitBootstrapDependency()
    {
        // AD-020: Step 6 has NO declared DependsOn but implicitly requires
        // Bootstrap metadata (CliOutput, CliVersion) and CLI version file on disk.
        // This test documents the gap — if Step 6 gains explicit dependencies, update this.
        var step = new HorizontalArticlesStep();

        Assert.Empty(step.DependsOn);
    }

    [Fact]
    public void Step6_RequiresAiConfiguration()
    {
        var step = new HorizontalArticlesStep();
        var definition = (StepDefinition)step;

        Assert.True(definition.RequiresAiConfiguration);
    }

    [Fact]
    public void Step6_DeclaresHorizontalArticlesOutput()
    {
        var step = new HorizontalArticlesStep();

        Assert.Contains("horizontal-articles", step.ExpectedOutputs);
    }

    // ── Dependency chain integrity ────────────────────────────────────

    [Fact]
    public void DependencyChain_Step1ToStep2_Step2DependsOn1()
    {
        var step2 = new ExamplePromptsStep();

        Assert.Contains(1, step2.DependsOn);
    }

    [Fact]
    public void DependencyChain_Step2ToStep3_Step3DependsOn1And2()
    {
        var step3 = new ToolGenerationStep();

        Assert.Contains(1, step3.DependsOn);
        Assert.Contains(2, step3.DependsOn);
    }

    [Fact]
    public void DependencyChain_Step3ToStep4_Step4DependsOn3()
    {
        var step4 = new ToolFamilyCleanupStep();

        Assert.Contains(3, step4.DependsOn);
    }

    [Fact]
    public void DependencyChain_Step4ToStep5_Step5HasNoDeclaredDep()
    {
        // Steps 4 and 5 are independent — Step 5 can run without Step 4.
        // Both implicitly require Bootstrap metadata, but Step 5 does not
        // consume Step 4 outputs.
        var step5 = new SkillsRelevanceStep();

        Assert.DoesNotContain(4, step5.DependsOn);
    }

    [Fact]
    public void DependencyChain_Step4ToStep6_Step6HasNoDeclaredDep()
    {
        // Step 6 does not declare dependency on Step 4 outputs.
        var step6 = new HorizontalArticlesStep();

        Assert.DoesNotContain(4, step6.DependsOn);
    }

    // ── No circular dependencies ──────────────────────────────────────

    [Fact]
    public void AllSteps_HaveNoCircularDependencies()
    {
        var steps = CreateAllSteps();
        var stepMap = steps.ToDictionary(s => s.Id);

        foreach (var step in steps)
        {
            var visited = new HashSet<int>();
            Assert.True(
                HasNoCycle(step.Id, stepMap, visited),
                $"Circular dependency detected involving step {step.Id} ({step.Name}).");
        }
    }

    [Fact]
    public void AllSteps_DependOnlyOnLowerNumberedSteps()
    {
        var steps = CreateAllSteps();

        foreach (var step in steps)
        {
            foreach (var depId in step.DependsOn)
            {
                Assert.True(
                    depId < step.Id,
                    $"Step {step.Id} ({step.Name}) declares dependency on step {depId} which is not lower-numbered. Dependencies must flow forward.");
            }
        }
    }

    [Fact]
    public void AllSteps_DependOnlyOnRegisteredStepIds()
    {
        var steps = CreateAllSteps();
        var registeredIds = steps.Select(s => s.Id).ToHashSet();

        foreach (var step in steps)
        {
            foreach (var depId in step.DependsOn)
            {
                Assert.True(
                    registeredIds.Contains(depId),
                    $"Step {step.Id} ({step.Name}) declares dependency on step {depId} which is not a registered step.");
            }
        }
    }

    // ── Expected outputs are non-empty for every step ─────────────────

    [Fact]
    public void AllNamespaceSteps_DeclareAtLeastOneExpectedOutput()
    {
        var steps = CreateAllSteps()
            .Where(s => s.Scope == StepScope.Namespace)
            .Cast<StepDefinition>();

        foreach (var step in steps)
        {
            Assert.True(
                step.ExpectedOutputs.Count > 0,
                $"Step {step.Id} ({step.Name}) declares no expected outputs.");
        }
    }

    [Fact]
    public void BootstrapStep_DeclaresAtLeastOneExpectedOutput()
    {
        var step = new BootstrapStep();
        var definition = (StepDefinition)step;

        Assert.True(definition.ExpectedOutputs.Count > 0);
    }

    // ── Implicit dependency documentation (AD-020) ────────────────────

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public void Steps5And6_RequireBootstrapMetadata_ButDontDeclareIt(int stepId)
    {
        // AD-020: These steps inherit from NamespaceStepBase whose ResolveTarget() reads
        // context.CliOutput and context.CliVersion — both set exclusively by Bootstrap.
        // This is an implicit dependency that should be made explicit.
        var registry = CreateDefaultRegistry();
        var step = registry.GetStep(stepId);

        Assert.Empty(step.DependsOn);
        Assert.Equal(StepScope.Namespace, step.Scope);
    }

    // ── Registry consistency ──────────────────────────────────────────

    [Fact]
    public void DefaultRegistry_BootstrapIsAlwaysIncluded_InGetOrderedSteps()
    {
        var registry = CreateDefaultRegistry();

        // Even when requesting only step 5, Bootstrap (step 0) is included
        var steps = registry.GetOrderedSteps([5]);

        Assert.Contains(steps, s => s.Id == 0);
        Assert.Contains(steps, s => s.Id == 5);
    }

    [Fact]
    public void DefaultRegistry_StepIdsAreContiguous_0Through6()
    {
        var registry = CreateDefaultRegistry();
        var ids = registry.GetAllSteps().Select(s => s.Id).OrderBy(id => id).ToArray();

        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, ids);
    }

    [Theory]
    [InlineData(0, typeof(BootstrapStep))]
    [InlineData(1, typeof(AnnotationsParametersRawStep))]
    [InlineData(2, typeof(ExamplePromptsStep))]
    [InlineData(3, typeof(ToolGenerationStep))]
    [InlineData(4, typeof(ToolFamilyCleanupStep))]
    [InlineData(5, typeof(SkillsRelevanceStep))]
    [InlineData(6, typeof(HorizontalArticlesStep))]
    public void DefaultRegistry_StepTypesMatchExpectedIds(int expectedId, Type expectedType)
    {
        var registry = CreateDefaultRegistry();
        var step = registry.GetStep(expectedId);

        Assert.IsType(expectedType, step);
        Assert.Equal(expectedId, step.Id);
    }

    // ── Transitive dependency completeness ─────────────────────────────

    [Fact]
    public void Step4_TransitivelyDependsOnSteps1And2_ThroughStep3()
    {
        // Step 4 → [3], Step 3 → [1, 2], so Step 4 transitively needs 1, 2, 3
        var steps = CreateAllSteps().ToDictionary(s => s.Id);
        var transitiveDeps = GetTransitiveDependencies(4, steps);

        Assert.Contains(3, transitiveDeps);
        Assert.Contains(2, transitiveDeps);
        Assert.Contains(1, transitiveDeps);
    }

    [Fact]
    public void Step3_TransitivelyDependsOnStep1_ThroughStep2()
    {
        // Step 3 → [1, 2], Step 2 → [1], so transitive deps = {1, 2}
        var steps = CreateAllSteps().ToDictionary(s => s.Id);
        var transitiveDeps = GetTransitiveDependencies(3, steps);

        Assert.Contains(1, transitiveDeps);
        Assert.Contains(2, transitiveDeps);
    }

    // ── helpers ────────────────────────────────────────────────────────

    private static StepRegistry CreateDefaultRegistry()
    {
        var scriptsRoot = Path.Combine(Path.GetTempPath(), $"contract-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(scriptsRoot);
        try
        {
            return StepRegistry.CreateDefault(scriptsRoot);
        }
        finally
        {
            Directory.Delete(scriptsRoot, recursive: true);
        }
    }

    private static IReadOnlyList<IPipelineStep> CreateAllSteps()
        => CreateDefaultRegistry().GetAllSteps();

    private static bool HasNoCycle(int stepId, IReadOnlyDictionary<int, IPipelineStep> stepMap, HashSet<int> visited)
    {
        if (!visited.Add(stepId))
            return false; // cycle detected

        if (!stepMap.TryGetValue(stepId, out var step))
            return true;

        foreach (var depId in step.DependsOn)
        {
            if (!HasNoCycle(depId, stepMap, new HashSet<int>(visited)))
                return false;
        }

        return true;
    }

    private static HashSet<int> GetTransitiveDependencies(int stepId, IReadOnlyDictionary<int, IPipelineStep> stepMap)
    {
        var result = new HashSet<int>();
        var queue = new Queue<int>(stepMap[stepId].DependsOn);

        while (queue.Count > 0)
        {
            var depId = queue.Dequeue();
            if (result.Add(depId) && stepMap.TryGetValue(depId, out var depStep))
            {
                foreach (var transitiveDep in depStep.DependsOn)
                    queue.Enqueue(transitiveDep);
            }
        }

        return result;
    }
}
