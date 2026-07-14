namespace PipelineRunner.Services;

/// <summary>
/// Loads namespace-to-filename mappings from config/namespace-mapping.json.
/// </summary>
public interface INamespaceMappingLoader
{
    /// <summary>
    /// Loads the namespace-to-filename mapping dictionary.
    /// </summary>
    /// <param name="repoRoot">Repository root directory containing config/namespace-mapping.json.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping namespace (e.g., "storage") to filename (e.g., "azure-storage.md").</returns>
    Task<IReadOnlyDictionary<string, string>> LoadAsync(string repoRoot, CancellationToken cancellationToken);
}
