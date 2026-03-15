namespace PipelineRunner.Contracts;

public sealed record StepResult(
    bool Success,
    IReadOnlyList<string> Warnings,
    TimeSpan Duration,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<string> ProcessInvocations,
    IReadOnlyList<ValidatorResult> ValidatorResults,
    IReadOnlyList<ArtifactFailure> ArtifactFailures,
    int? ExitCodeOverride = null)
{
    public static StepResult DryRun(IEnumerable<string> outputs)
        => new(true, Array.Empty<string>(), TimeSpan.Zero, outputs.ToArray(), Array.Empty<string>(), Array.Empty<ValidatorResult>(), Array.Empty<ArtifactFailure>());
}
