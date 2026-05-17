namespace PipelineRunner.Services;

/// <summary>
/// Evaluates whether a namespace should be processed by checking the upstream CHANGELOG
/// for entries mentioning it in versions at or newer than the current CLI version.
/// </summary>
public interface IChangelogGate
{
    /// <summary>
    /// Evaluates the gate for the given namespace.
    /// </summary>
    /// <param name="namespaceName">Namespace to check (e.g. "compute").</param>
    /// <param name="baselineVersion">Current CLI version string (e.g. "1.2.3"). Sections at or newer than this version are checked.</param>
    /// <param name="mcpBranch">Branch of microsoft/mcp to fetch CHANGELOG from.</param>
    /// <param name="hasExistingArticle">True when the namespace already has a generated article on disk. New namespaces always process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="ChangelogGateResult"/> with ShouldSkip=true when no CHANGELOG entries are found.</returns>
    Task<ChangelogGateResult> EvaluateAsync(
        string namespaceName,
        string baselineVersion,
        string mcpBranch,
        bool hasExistingArticle,
        CancellationToken cancellationToken);
}
