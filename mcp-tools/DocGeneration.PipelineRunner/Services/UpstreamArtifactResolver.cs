namespace PipelineRunner.Services;

using Shared;

/// <summary>
/// Resolves upstream step outputs by reading their step-result.json envelopes.
/// Falls back to legacy path resolution when no envelope exists.
/// </summary>
public sealed class UpstreamArtifactResolver
{
    /// <summary>
    /// Attempts to resolve an upstream step's output directory from its envelope.
    /// Returns the envelope if found, null otherwise.
    /// </summary>
    /// <exception cref="StepResultSchemaException">
    /// Propagated from <see cref="StepResultReader.TryRead(string, out StepResultFile?)"/>
    /// when the upstream envelope declares an unsupported schema version.
    /// </exception>
    public StepResultFile? TryReadUpstream(string outputPath, int stepId, string stepSlug)
    {
        var envelopeDir = Path.Combine(outputPath, $"step-{stepId}-{stepSlug}");
        if (StepResultReader.TryRead(envelopeDir, out var result))
            return result;

        return null;
    }

    /// <summary>
    /// Gets the workspace directory for an upstream step.
    /// </summary>
    public string GetUpstreamWorkspaceDir(string outputPath, int stepId, string stepSlug)
        => Path.Combine(outputPath, $"step-{stepId}-{stepSlug}");

    /// <summary>
    /// Attempts to resolve a logical output directory from the upstream envelope's output artifacts.
    /// The requested directory can appear anywhere within the artifact path to support gradual migration.
    /// </summary>
    public bool TryResolveOutputDirectory(
        string outputPath,
        StepResultFile? envelope,
        string relativeDirectory,
        out string resolvedDirectory)
    {
        resolvedDirectory = string.Empty;
        var relativePath = TryResolveRelativePath(envelope, relativeDirectory);
        if (relativePath is null)
            return false;

        resolvedDirectory = Path.GetFullPath(Path.Combine(outputPath, relativePath));
        return true;
    }

    /// <summary>
    /// Attempts to resolve a logical output file from the upstream envelope's output artifacts.
    /// The requested file path can appear anywhere within the artifact path to support gradual migration.
    /// </summary>
    public bool TryResolveOutputFile(
        string outputPath,
        StepResultFile? envelope,
        string relativeFilePath,
        out string resolvedFile)
    {
        resolvedFile = string.Empty;
        var relativePath = TryResolveRelativePath(envelope, relativeFilePath);
        if (relativePath is null)
            return false;

        resolvedFile = Path.GetFullPath(Path.Combine(outputPath, relativePath));
        return true;
    }

    private static string? TryResolveRelativePath(StepResultFile? envelope, string requestedPath)
    {
        if (envelope?.OutputArtifacts is not { Count: > 0 })
            return null;

        var requestedSegments = SplitPathSegments(requestedPath);
        if (requestedSegments.Length == 0)
            return null;

        foreach (var artifact in envelope.OutputArtifacts)
        {
            if (string.IsNullOrWhiteSpace(artifact.Path))
                continue;

            var artifactSegments = SplitPathSegments(artifact.Path);
            var matchIndex = FindSegmentSequence(artifactSegments, requestedSegments);
            if (matchIndex < 0)
                continue;

            return string.Join(
                Path.DirectorySeparatorChar,
                artifactSegments.Take(matchIndex + requestedSegments.Length));
        }

        return null;
    }

    private static string[] SplitPathSegments(string path)
        => path.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static int FindSegmentSequence(IReadOnlyList<string> haystack, IReadOnlyList<string> needle)
    {
        if (needle.Count == 0 || haystack.Count < needle.Count)
            return -1;

        for (var start = 0; start <= haystack.Count - needle.Count; start++)
        {
            var matched = true;
            for (var offset = 0; offset < needle.Count; offset++)
            {
                if (!string.Equals(haystack[start + offset], needle[offset], StringComparison.OrdinalIgnoreCase))
                {
                    matched = false;
                    break;
                }
            }

            if (matched)
                return start;
        }

        return -1;
    }
}
