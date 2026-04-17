using System.Text.Json;

namespace PipelineRunner.Services;

public sealed class FilteredCliWriter : IFilteredCliWriter
{
    private readonly IWorkspaceManager _workspaceManager;

    public FilteredCliWriter(IWorkspaceManager workspaceManager)
    {
        _workspaceManager = workspaceManager;
    }

    public async ValueTask<FilteredCliFileHandle> WriteAsync(
        CliMetadataSnapshot cliOutput,
        IReadOnlyList<CliTool> matchingTools,
        string tempDirectoryName,
        CancellationToken cancellationToken)
    {
        var temporaryDirectory = _workspaceManager.CreateTemporaryDirectory(tempDirectoryName);
        var filePath = Path.Combine(temporaryDirectory, "cli-output-single-tool.json");

        var version = cliOutput.RawRoot.TryGetProperty("version", out var versionProperty)
            ? versionProperty.Clone()
            : JsonDocument.Parse("null").RootElement.Clone();

        var payload = new FilteredCliPayload(version, matchingTools.Select(tool => tool.Raw.Clone()).ToArray());

        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, payload, cancellationToken: cancellationToken);
        return new FilteredCliFileHandle(temporaryDirectory, filePath);
    }

    private sealed record FilteredCliPayload(JsonElement Version, IReadOnlyList<JsonElement> Results);
}
