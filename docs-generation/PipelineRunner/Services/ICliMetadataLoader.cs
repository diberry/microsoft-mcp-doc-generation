using System.Text.Json;

namespace PipelineRunner.Services;

public sealed record CliTool(string Command, string Name, string? Description, JsonElement Raw);

public sealed record CliMetadataSnapshot(string FilePath, JsonElement RawRoot, IReadOnlyList<CliTool> Tools);

public interface ICliMetadataLoader
{
    bool CliOutputExists(string outputPath);

    bool NamespaceMetadataExists(string outputPath);

    ValueTask<CliMetadataSnapshot> LoadCliOutputAsync(string outputPath, CancellationToken cancellationToken);

    ValueTask<string> LoadCliVersionAsync(string outputPath, CancellationToken cancellationToken);

    ValueTask<IReadOnlyList<string>> LoadNamespacesAsync(string outputPath, CancellationToken cancellationToken);
}
