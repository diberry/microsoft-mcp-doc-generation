namespace PipelineRunner.Contracts;

public sealed record ArtifactFailure(
    string ArtifactType,
    string ArtifactName,
    string Summary,
    IReadOnlyList<string> Details,
    IReadOnlyList<string> RelatedPaths)
{
    public static ArtifactFailure Create(
        string artifactType,
        string artifactName,
        string summary,
        IEnumerable<string>? details = null,
        IEnumerable<string>? relatedPaths = null)
        => new(
            artifactType,
            artifactName,
            summary,
            Clean(details),
            Clean(relatedPaths));

    private static string[] Clean(IEnumerable<string>? values)
        => values?
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? Array.Empty<string>();
}
