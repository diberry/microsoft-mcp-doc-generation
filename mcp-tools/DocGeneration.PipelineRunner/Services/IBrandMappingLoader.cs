namespace PipelineRunner.Services;

public interface IBrandMappingLoader
{
    /// <summary>
    /// Loads all entries from <c>mcp-tools/data/brand-to-server-mapping.json</c>.
    /// Returns an empty list if the file does not exist.
    /// </summary>
    Task<IReadOnlyList<BrandMappingEntry>> LoadAsync(string mcpToolsRoot, CancellationToken cancellationToken);
}
