using System.Text.Json;

namespace PipelineRunner.Services;

public sealed class BrandMappingLoader : IBrandMappingLoader
{
    private const string RelativePath = "data/brand-to-server-mapping.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IReadOnlyList<BrandMappingEntry>> LoadAsync(string mcpToolsRoot, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(mcpToolsRoot, RelativePath);
        if (!File.Exists(filePath))
        {
            return [];
        }

        await using var stream = File.OpenRead(filePath);
        var entries = await JsonSerializer.DeserializeAsync<BrandMappingEntry[]>(stream, JsonOptions, cancellationToken);
        return entries ?? [];
    }
}
