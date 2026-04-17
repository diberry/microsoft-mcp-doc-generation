using System.Text.Json;

namespace PipelineRunner.Services;

public sealed record FilteredCliFileHandle(string TemporaryDirectory, string FilePath);

public interface IFilteredCliWriter
{
    ValueTask<FilteredCliFileHandle> WriteAsync(
        CliMetadataSnapshot cliOutput,
        IReadOnlyList<CliTool> matchingTools,
        string tempDirectoryName,
        CancellationToken cancellationToken);
}
