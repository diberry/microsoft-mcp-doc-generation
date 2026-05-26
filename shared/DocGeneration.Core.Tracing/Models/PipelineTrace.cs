namespace DocGeneration.Core.Tracing.Models;

public sealed record PipelineTrace
{
    public required string PipelineName { get; init; }
    public required string RunId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset EndedAt { get; init; }
    public required long DurationMs { get; init; }
    public required IReadOnlyList<TraceEvent> Steps { get; init; }
    public int TotalSteps => Steps.Count;
}
