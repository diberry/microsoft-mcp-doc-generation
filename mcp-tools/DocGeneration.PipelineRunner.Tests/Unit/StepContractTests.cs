using PipelineRunner.Contracts;
using PipelineRunner.Registry;
using PipelineRunner.Steps;
using Xunit;

namespace PipelineRunner.Tests.Unit;

/// <summary>
/// Contract tests verifying structural guarantees of the typed pipeline steps.
/// These tests catch regressions when step IDs, dependencies, scopes, or outputs change.
/// See issue #208.
/// </summary>
public class StepContractTests
{
    private readonly StepRegistry _registry;
    private readonly IReadOnlyList<IPipelineStep> _allSteps;

    public StepContractTests()
    {
        _registry = StepRegistry.CreateDefault(scriptsRoot: ".");
        _allSteps = _registry.GetAllSteps();
    }

    // ── Unique IDs ──────────────────────────────────────────────────────

    [Fact]
    public void AllSteps_HaveUniqueIds()
    {
        var ids = _allSteps.Select(s => s.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void AllSteps_IdsAreContiguousFromZeroToSix()
    {
        var ids = _allSteps.Select(s => s.Id).OrderBy(id => id).ToArray();
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6 }, ids);
    }

    [Fact]
    public void Registry_ReturnsExactly7Steps()
    {
        Assert.Equal(7, _allSteps.Count);
    }

    // ── Step scope ──────────────────────────────────────────────────────

    [Fact]
    public void Step0_IsGlobalScope()
    {
        Assert.Equal(StepScope.Global, _registry.GetStep(0).Scope);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void NamespaceSteps_AreNamespaceScope(int stepId)
    {
        Assert.Equal(StepScope.Namespace, _registry.GetStep(stepId).Scope);
    }

    // ── Dependency chains ───────────────────────────────────────────────

    [Fact]
    public void Step0_HasNoDependencies()
    {
        Assert.Empty(_registry.GetStep(0).DependsOn);
    }

    [Fact]
    public void Step1_HasNoDependencies()
    {
        Assert.Empty(_registry.GetStep(1).DependsOn);
    }

    [Fact]
    public void Step2_DependsOnStep1()
    {
        Assert.Equal(new[] { 1 }, _registry.GetStep(2).DependsOn);
    }

    [Fact]
    public void Step3_DependsOnSteps1And2()
    {
        Assert.Equal(new[] { 1, 2 }, _registry.GetStep(3).DependsOn);
    }

    [Fact]
    public void Step4_DependsOnStep3()
    {
        // Step 4 declares only a direct dependency on Step 3.
        // Steps 1 and 2 are reached transitively through Step 3's own deps.
        Assert.Equal(new[] { 3 }, _registry.GetStep(4).DependsOn);
    }

    /// <summary>
    /// Steps 5 and 6 consume CLI metadata produced by Bootstrap (Step 0).
    /// Without this dependency, running them in isolation (e.g. ./start.sh advisor 5)
    /// throws a confusing InvalidOperationException instead of a clear dependency error.
    /// See issue #187 item 3.
    /// </summary>
    [Fact]
    public void Step5_DependsOnBootstrap()
    {
        Assert.Contains(0, _registry.GetStep(5).DependsOn);
    }

    [Fact]
    public void Step6_DependsOnBootstrap()
    {
        Assert.Contains(0, _registry.GetStep(6).DependsOn);
    }

    // ── No circular dependencies ────────────────────────────────────────

    [Fact]
    public void NoDependencyCycles()
    {
        foreach (var step in _allSteps)
        {
            var visited = new HashSet<int>();
            var queue = new Queue<int>(step.DependsOn);

            while (queue.Count > 0)
            {
                var depId = queue.Dequeue();
                Assert.NotEqual(step.Id, depId); // cycle back to self

                if (!visited.Add(depId))
                    continue;

                var depStep = _registry.GetStep(depId);
                foreach (var transitive in depStep.DependsOn)
                    queue.Enqueue(transitive);
            }
        }
    }

    [Fact]
    public void AllDependencies_ReferenceExistingStepIds()
    {
        var validIds = _allSteps.Select(s => s.Id).ToHashSet();

        foreach (var step in _allSteps)
        {
            foreach (var depId in step.DependsOn)
            {
                Assert.Contains(depId, validIds);
            }
        }
    }

    // ── Expected outputs ────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void EachStep_HasNonEmptyExpectedOutputs(int stepId)
    {
        var step = _registry.GetStep(stepId) as StepDefinition;
        Assert.NotNull(step);
        Assert.NotEmpty(step.ExpectedOutputs);
    }

    [Fact]
    public void Step0_ExpectedOutputs_IncludeCliAndTools()
    {
        var step = (StepDefinition)_registry.GetStep(0);
        Assert.Contains("cli", step.ExpectedOutputs);
        Assert.Contains("tools", step.ExpectedOutputs);
    }

    [Fact]
    public void Step1_ExpectedOutputs_IncludeAnnotationsAndParameters()
    {
        var step = (StepDefinition)_registry.GetStep(1);
        Assert.Contains("annotations", step.ExpectedOutputs);
        Assert.Contains("parameters", step.ExpectedOutputs);
    }

    [Fact]
    public void Step4_ExpectedOutputs_IncludeToolFamily()
    {
        var step = (StepDefinition)_registry.GetStep(4);
        Assert.Contains("tool-family", step.ExpectedOutputs);
    }

    // ── Step names ──────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public void EachStep_HasNonEmptyName(int stepId)
    {
        Assert.False(string.IsNullOrWhiteSpace(_registry.GetStep(stepId).Name));
    }

    // ── Step types ──────────────────────────────────────────────────────

    [Fact]
    public void Step0_IsBootstrapStep()
    {
        Assert.IsType<BootstrapStep>(_registry.GetStep(0));
    }

    [Fact]
    public void Step1_IsAnnotationsParametersRawStep()
    {
        Assert.IsType<AnnotationsParametersRawStep>(_registry.GetStep(1));
    }

    [Fact]
    public void Step5_IsSkillsRelevanceStep()
    {
        Assert.IsType<SkillsRelevanceStep>(_registry.GetStep(5));
    }

    [Fact]
    public void Step6_IsHorizontalArticlesStep()
    {
        Assert.IsType<HorizontalArticlesStep>(_registry.GetStep(6));
    }

    // ── AI configuration requirements ───────────────────────────────────

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void AiSteps_RequireAiConfiguration(int stepId)
    {
        var step = _registry.GetStep(stepId) as StepDefinition;
        Assert.NotNull(step);
        Assert.True(step.RequiresAiConfiguration,
            $"Step {stepId} ({step.Name}) should require AI configuration");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public void NonAiSteps_DoNotRequireAiConfiguration(int stepId)
    {
        var step = _registry.GetStep(stepId) as StepDefinition;
        Assert.NotNull(step);
        Assert.False(step.RequiresAiConfiguration,
            $"Step {stepId} ({step.Name}) should not require AI configuration");
    }

    // ── Failure policy ──────────────────────────────────────────────────

    [Fact]
    public void Step5_IsWarnOnly()
    {
        Assert.Equal(FailurePolicy.Warn, _registry.GetStep(5).FailurePolicy);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(6)]
    public void NonOptionalSteps_AreFatal(int stepId)
    {
        Assert.Equal(FailurePolicy.Fatal, _registry.GetStep(stepId).FailurePolicy);
    }

    // ── Retry policy ────────────────────────────────────────────────────

    [Fact]
    public void Step4_AllowsRetries()
    {
        Assert.True(_registry.GetStep(4).MaxRetries > 0,
            "Step 4 (ToolFamilyCleanup) should allow retries");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(6)]
    public void OtherSteps_HaveNoRetries(int stepId)
    {
        Assert.Equal(0, _registry.GetStep(stepId).MaxRetries);
    }
}
