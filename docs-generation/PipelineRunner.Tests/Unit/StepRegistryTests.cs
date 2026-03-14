using PipelineRunner.Contracts;
using PipelineRunner.Context;
using PipelineRunner.Registry;
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
