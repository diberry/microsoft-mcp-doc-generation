using PipelineRunner.Contracts;
using PipelineRunner.Context;
using PipelineRunner.Registry;
using PipelineRunner.Steps;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public class StepRegistryTests
{
    [Fact]
    public void Constructor_DuplicateIds_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new StepRegistry([
            new FakeStep(1, "first"),
            new FakeStep(1, "duplicate"),
        ]));
    }

    [Fact]
    public void GetOrderedSteps_ReturnsStepsSortedById()
    {
        var registry = new StepRegistry([
            new FakeStep(4, "fourth"),
            new FakeStep(2, "second"),
            new FakeStep(1, "first"),
        ]);

        var steps = registry.GetOrderedSteps([4, 1, 2]);

        Assert.Equal(new[] { 1, 2, 4 }, steps.Select(step => step.Id));
    }

    [Fact]
    public void GetStep_KnownId_ReturnsRegisteredStep()
    {
        var registry = new StepRegistry([
            new FakeStep(2, "second"),
        ]);

        var step = registry.GetStep(2);

        Assert.Equal("second", step.Name);
    }

    [Fact]
    public void CreateDefault_RegistersTypedPhaseThreeSteps()
    {
        var scriptsRoot = Path.Combine(Path.GetTempPath(), $"pipeline-runner-scripts-{Guid.NewGuid():N}");
        Directory.CreateDirectory(scriptsRoot);

        try
        {
            var registry = StepRegistry.CreateDefault(scriptsRoot);

            Assert.IsType<AnnotationsParametersRawStep>(registry.GetStep(1));
            Assert.IsType<ExamplePromptsStep>(registry.GetStep(2));
            Assert.IsType<ToolGenerationStep>(registry.GetStep(3));
            Assert.IsType<ToolFamilyCleanupStep>(registry.GetStep(4));
            Assert.IsType<ShimStep>(registry.GetStep(5));
            Assert.IsType<HorizontalArticlesStep>(registry.GetStep(6));
        }
        finally
        {
            Directory.Delete(scriptsRoot, recursive: true);
        }
    }

    private sealed class FakeStep : IPipelineStep
    {
        public FakeStep(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }

        public string Name { get; }

        public StepScope Scope => StepScope.Namespace;

        public FailurePolicy FailurePolicy => FailurePolicy.Fatal;

        public IReadOnlyList<int> DependsOn => Array.Empty<int>();

        public IReadOnlyList<IPostValidator> PostValidators => Array.Empty<IPostValidator>();

        public ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(StepResult.DryRun(Array.Empty<string>()));
    }
}
