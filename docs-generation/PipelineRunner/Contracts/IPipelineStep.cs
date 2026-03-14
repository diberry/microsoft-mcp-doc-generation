using PipelineRunner.Context;

namespace PipelineRunner.Contracts;

public interface IPipelineStep
{
    int Id { get; }

    string Name { get; }

    StepScope Scope { get; }

    FailurePolicy FailurePolicy { get; }

    IReadOnlyList<int> DependsOn { get; }

    IReadOnlyList<IPostValidator> PostValidators { get; }

    ValueTask<StepResult> ExecuteAsync(PipelineContext context, CancellationToken cancellationToken);
}
