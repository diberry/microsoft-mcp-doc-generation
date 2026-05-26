namespace DocGeneration.Core.Tracing;

public sealed record AiInteractionRecord
{
    public required string SkillOrToolName { get; init; }
    public required string Operation { get; init; }
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    public required string ResponseContent { get; init; }
    public required string Model { get; init; }
    public int? TotalTokens { get; init; }
    public long DurationMs { get; init; }
    public int RetryCount { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
}
