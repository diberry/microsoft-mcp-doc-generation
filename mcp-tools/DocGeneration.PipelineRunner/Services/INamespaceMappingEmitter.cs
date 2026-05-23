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
    /// </summary>
    /// <param name="brandMappings">Resolved brand mapping entries from <c>brand-to-server-mapping.json</c>.</param>
    /// <param name="cliOutput">CLI tool metadata extracted during bootstrap.</param>
    /// <param name="cliVersion">Version string from <c>cli-version.json</c> (value of <c>context.CliVersion</c>).</param>
    /// <param name="outputPath">Root output directory (e.g. <c>generated/</c>).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task EmitAsync(
        IReadOnlyList<BrandMappingEntry> brandMappings,
        CliMetadataSnapshot cliOutput,
        string cliVersion,
        string outputPath,
        CancellationToken cancellationToken);
}
