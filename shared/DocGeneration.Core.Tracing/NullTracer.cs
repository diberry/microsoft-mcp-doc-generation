namespace DocGeneration.Core.Tracing;

public sealed class NullTracer : IPipelineTracer
{
    public static readonly NullTracer Instance = new();

    private NullTracer()
    {
    }

    public IStepHandle StartStep(string stepName, StepClassification stepType, string? targetName = null, string? inputSummary = null) => NullStepHandle.Instance;

    public void RecordAiCall(AiInteractionRecord record)
    {
    }

    public Task FlushAsync(string outputDirectory, CancellationToken ct = default) => Task.CompletedTask;

    private sealed class NullStepHandle : IStepHandle
    {
        public static readonly NullStepHandle Instance = new();

        public void Complete(string? outputSummary = null)
        {
        }

        public void Fail(string error)
        {
        }

        public void Dispose()
        {
        }
    }
}
