namespace PipelineRunner.Contracts;

/// <summary>
/// Records that a namespace step could not produce or validate a specific artifact.
/// </summary>
/// <param name="IsBlocking">
/// Whether this failure must make its step a fatal root (AD-029 §A2 "C2") that suppresses transitive
/// dependents. Defaults to <c>true</c> so every pre-existing call site (which never specified this
/// value) keeps its original hard-failure semantics unchanged. Set to <c>false</c> only for failures
/// that are genuinely a content/parameter-validation warning — never for process, launch, or missing
/// required-artifact failures — so they remain visible (recorded here, and in warnings/console output)
/// without blocking the rest of the pipeline. See
/// <see cref="global::PipelineRunner.PipelineRunner.IsFatalRoot"/>.
/// </param>
public sealed record ArtifactFailure(
    string ArtifactType,
    string ArtifactName,
    string Summary,
    IReadOnlyList<string> Details,
    IReadOnlyList<string> RelatedPaths,
    bool IsBlocking = true)
{
    public static ArtifactFailure Create(
        string artifactType,
        string artifactName,
        string summary,
        IEnumerable<string>? details = null,
        IEnumerable<string>? relatedPaths = null,
        bool isBlocking = true)
        => new(
            artifactType,
            artifactName,
            summary,
            Clean(details),
            Clean(relatedPaths),
            isBlocking);

    private static string[] Clean(IEnumerable<string>? values)
        => values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<string>();
}
