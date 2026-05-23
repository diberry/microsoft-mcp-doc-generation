using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PipelineRunner.Services;

/// <summary>
/// Builds and writes <c>namespace-mapping.json</c> to the root of the global output directory.
/// The file is a versioned snapshot of the brand mapping + tool assignment, consumed by downstream
/// validation agents and coverage audits (see PRD issue #618).
/// </summary>
public sealed class NamespaceMappingEmitter : INamespaceMappingEmitter
{
    /// <summary>Output filename, placed at the root of the global output directory.</summary>
    internal const string OutputFileName = "namespace-mapping.json";

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    /// <inheritdoc />
    public async Task EmitAsync(
        IReadOnlyList<BrandMappingEntry> brandMappings,
        CliMetadataSnapshot cliOutput,
        string cliVersion,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var namespaces = new Dictionary<string, NamespaceEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in brandMappings)
        {
            if (string.IsNullOrWhiteSpace(mapping.McpServerName))
            {
                continue;
            }

            var normalizedNs = mapping.McpServerName.Replace('_', ' ');

            var matchingTools = cliOutput.Tools
                .Where(tool =>
                    string.Equals(tool.Command, normalizedNs, StringComparison.OrdinalIgnoreCase)
                    || tool.Command.StartsWith($"{normalizedNs} ", StringComparison.OrdinalIgnoreCase))
                .Select(tool => string.IsNullOrEmpty(tool.Name) ? tool.Command : tool.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            namespaces[mapping.McpServerName] = new NamespaceEntry(
                mapping.BrandName,
                mapping.FileName,
                mapping.ShortName,
                mapping.MergeGroup,
                matchingTools);
        }

        var document = new NamespaceMappingDocument(
            DateTime.UtcNow.ToString("O"),
            cliVersion,
            namespaces.Count,
            namespaces.Values.Sum(entry => entry.Tools.Length),
            namespaces);

        var outputFilePath = Path.Combine(outputPath, OutputFileName);
        var json = JsonSerializer.Serialize(document, WriteOptions);
        await File.WriteAllTextAsync(outputFilePath, json, Encoding.UTF8, cancellationToken);
    }

    private sealed record NamespaceMappingDocument(
        [property: JsonPropertyName("generated_at")] string GeneratedAt,
        [property: JsonPropertyName("source_version")] string SourceVersion,
        [property: JsonPropertyName("namespace_count")] int NamespaceCount,
        [property: JsonPropertyName("tool_count")] int ToolCount,
        [property: JsonPropertyName("namespaces")] Dictionary<string, NamespaceEntry> Namespaces);

    private sealed record NamespaceEntry(
        [property: JsonPropertyName("display_name")] string DisplayName,
        [property: JsonPropertyName("file_name")] string FileName,
        [property: JsonPropertyName("short_name")] string ShortName,
        [property: JsonPropertyName("merge_group")] string? MergeGroup,
        [property: JsonPropertyName("tools")] string[] Tools);
}
