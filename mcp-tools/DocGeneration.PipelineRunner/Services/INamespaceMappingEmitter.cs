namespace PipelineRunner.Services;

/// <summary>
/// Emits a <c>namespace-mapping.json</c> file at the root of the global output directory.
/// The file joins validated brand mappings with the resolved CLI tool list, producing a
/// machine-readable snapshot of which tools belong to which namespace.
/// </summary>
public interface INamespaceMappingEmitter
{
    /// <summary>
    /// Builds and writes <c>namespace-mapping.json</c> to <paramref name="outputPath"/>.
    /// Returns the tool names that were not matched to any namespace prefix (may be empty).
    /// Callers should log a warning when the returned collection is non-empty.
    /// </summary>
    /// <param name="brandMappings">Resolved brand mapping entries from <c>brand-to-server-mapping.json</c>.</param>
    /// <param name="cliOutput">CLI tool metadata extracted during bootstrap.</param>
    /// <param name="cliVersion">Version string from <c>cli-version.json</c> (value of <c>context.CliVersion</c>).</param>
    /// <param name="outputPath">Root output directory (e.g. <c>generated/</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tool names present in CLI output that were not matched to any brand mapping namespace.</returns>
    Task<IReadOnlyList<string>> EmitAsync(
        IReadOnlyList<BrandMappingEntry> brandMappings,
        CliMetadataSnapshot cliOutput,
        string cliVersion,
        string outputPath,
        CancellationToken cancellationToken);
}
