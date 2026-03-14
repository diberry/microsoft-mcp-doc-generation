using PipelineRunner.Context;

namespace PipelineRunner.Contracts;

public interface IPostValidator
{
    string Name { get; }

    ValueTask<ValidatorResult> ValidateAsync(PipelineContext context, IPipelineStep step, CancellationToken cancellationToken);
}

public sealed record ValidatorResult(string Name, bool Success, IReadOnlyList<string> Warnings);
