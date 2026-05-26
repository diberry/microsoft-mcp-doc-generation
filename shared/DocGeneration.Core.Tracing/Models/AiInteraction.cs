namespace DocGeneration.Core.Tracing.Models;

public sealed record AiInteraction
{
    public required long SequenceNumber { get; init; }
    public required string SkillOrToolName { get; init; }
    public required string Operation { get; init; }
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    public required string ResponseContent { get; init; }
    public required string Model { get; init; }
    public int? TotalTokens { get; init; }
    public required long DurationMs { get; init; }
    public required int RetryCount { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
}
