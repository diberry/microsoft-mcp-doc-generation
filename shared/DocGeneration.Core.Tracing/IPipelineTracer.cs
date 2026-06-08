namespace DocGeneration.Core.Tracing;

public enum StepClassification
{
    AI,
    Deterministic,
    Hybrid
}

public enum StepStatus
{
    Completed,
    Failed,
    Incomplete
}

public interface IPipelineTracer
{
    IStepHandle StartStep(string stepName, StepClassification stepType, string? targetName = null, string? inputSummary = null);
    void RecordAiCall(AiInteractionRecord record);

    /// <summary>
    /// Writes buffered trace artifacts to the supplied directory.
    /// Call this from finally blocks so traces are preserved for success, failure, and cancellation paths.
    /// </summary>
    Task FlushAsync(string outputDirectory, CancellationToken ct = default);
}

public interface IStepHandle : IDisposable
{
    void Complete(string? outputSummary = null);
    void Fail(string error);
}
