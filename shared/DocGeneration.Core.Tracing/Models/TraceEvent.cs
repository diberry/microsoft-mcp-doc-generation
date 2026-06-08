namespace DocGeneration.Core.Tracing.Models;

public sealed record TraceEvent
{
    public required long SequenceNumber { get; init; }
    public required string StepName { get; init; }
    public required StepClassification StepType { get; init; }
    public required StepStatus Status { get; init; }
    public string? TargetName { get; init; }
    public string? InputSummary { get; init; }
    public string? OutputSummary { get; init; }
    public string? Error { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required DateTimeOffset EndedAt { get; init; }
    public required long DurationMs { get; init; }
}
