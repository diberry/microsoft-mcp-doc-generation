using System.Text.Json;
using PipelineRunner.Services;
using ToolFamilyCleanup.Services;

namespace PipelineRunner.Validation;

internal static class SourceCliMetadataResolver
{
    public static async ValueTask<SourceCliMetadataResolution> ResolveAsync(
        string repoRoot,
        string? configuredVersion,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(configuredVersion))
        {
            return SourceCliMetadataResolution.Failure("configured target version is unavailable; add mcp-tool-version.txt.");
        }

        var metadataRoot = Path.Combine(repoRoot, MetadataConstants.McpCliMetadataDirectoryName);
        if (!Directory.Exists(metadataRoot))
        {
            return SourceCliMetadataResolution.Failure($"source metadata directory was not found: {metadataRoot}");
        }

        var sourceDirectory = Directory.EnumerateDirectories(metadataRoot)
            .Where(path => string.Equals(
                SourceVersionVerificationGate.ExtractVersionFromSourcePath(Path.Combine(path, MetadataConstants.CliOutputFileName)),
                configuredVersion,
                StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?? ResolveTrackedOrOnlySourceDirectory(metadataRoot, cancellationToken);

        if (sourceDirectory is null)
        {
            return SourceCliMetadataResolution.Failure(
                $"source metadata folder for configured target version '{configuredVersion}' was not found under '{metadataRoot}'.");
        }

        var sourcePath = ResolveSourceFilePath(sourceDirectory);
        if (sourcePath is null)
        {
            return SourceCliMetadataResolution.Failure(
                $"source CLI JSON was not found in '{sourceDirectory}' (expected {MetadataConstants.CliOutputFileName} or {MetadataConstants.ToolsListFileName}).");
        }

        var rawJson = await File.ReadAllTextAsync(sourcePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return SourceCliMetadataResolution.Failure($"source CLI JSON is empty: {sourcePath}");
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            var rawRoot = document.RootElement.Clone();
            var results = ResolveResultsArray(rawRoot);
            var tools = results
                .EnumerateArray()
                .Select(tool => new CliTool(
                    tool.GetProperty("command").GetString() ?? string.Empty,
                    tool.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() ?? string.Empty : string.Empty,
                    tool.TryGetProperty("description", out var descriptionProperty) ? descriptionProperty.GetString() : null,
                    tool.Clone()))
                .ToArray();

            return SourceCliMetadataResolution.FromSnapshot(new CliMetadataSnapshot(sourcePath, rawRoot, tools));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return SourceCliMetadataResolution.Failure($"source CLI JSON could not be loaded from '{sourcePath}': {ex.Message}");
        }
    }

    private static string? ResolveSourceFilePath(string sourceDirectory)
    {
        var cliOutputPath = Path.Combine(sourceDirectory, MetadataConstants.CliOutputFileName);
        if (File.Exists(cliOutputPath))
        {
            return cliOutputPath;
        }

        var toolsListPath = Path.Combine(sourceDirectory, MetadataConstants.ToolsListFileName);
        return File.Exists(toolsListPath) ? toolsListPath : null;
    }

    private static string? ResolveTrackedOrOnlySourceDirectory(string metadataRoot, CancellationToken cancellationToken)
    {
        var trackedVersionPath = Path.Combine(metadataRoot, MetadataConstants.TrackedVersionFileName);
        if (File.Exists(trackedVersionPath))
        {
            var trackedVersion = File.ReadAllText(trackedVersionPath).Trim();
            if (!string.IsNullOrWhiteSpace(trackedVersion))
            {
                var trackedDirectory = Directory.EnumerateDirectories(metadataRoot)
                    .Where(path => string.Equals(
                        SourceVersionVerificationGate.ExtractVersionFromSourcePath(Path.Combine(path, MetadataConstants.CliOutputFileName)),
                        trackedVersion,
                        StringComparison.OrdinalIgnoreCase))
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (trackedDirectory is not null)
                {
                    return trackedDirectory;
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var sourceDirectories = Directory.EnumerateDirectories(metadataRoot)
            .Where(path => ResolveSourceFilePath(path) is not null)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Take(2)
            .ToArray();
        return sourceDirectories.Length == 1 ? sourceDirectories[0] : null;
    }

    private static JsonElement ResolveResultsArray(JsonElement rawRoot)
    {
        if (rawRoot.ValueKind == JsonValueKind.Array)
        {
            return rawRoot;
        }

        if (rawRoot.ValueKind == JsonValueKind.Object
            && rawRoot.TryGetProperty(MetadataConstants.ResultsPropertyName, out var results)
            && results.ValueKind == JsonValueKind.Array)
        {
            return results;
        }

        throw new InvalidOperationException("expected a top-level results array or bare tool array.");
    }
}

internal sealed record SourceCliMetadataResolution(bool Success, CliMetadataSnapshot? Snapshot, string? Error)
{
    public static SourceCliMetadataResolution FromSnapshot(CliMetadataSnapshot snapshot)
        => new(true, snapshot, null);

    public static SourceCliMetadataResolution Failure(string error)
        => new(false, null, error);
}
