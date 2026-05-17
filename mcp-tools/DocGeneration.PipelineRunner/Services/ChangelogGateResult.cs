namespace PipelineRunner.Services;

/// <summary>
/// Result of a <see cref="IChangelogGate"/> evaluation for a namespace.
/// </summary>
public sealed record ChangelogGateResult(bool ShouldSkip, string Reason)
{
    /// <summary>Creates a result indicating the namespace should be skipped.</summary>
    public static ChangelogGateResult Skip(string reason) => new(true, reason);

    /// <summary>Creates a result indicating the namespace should be processed.</summary>
    public static ChangelogGateResult Process(string reason) => new(false, reason);
}
