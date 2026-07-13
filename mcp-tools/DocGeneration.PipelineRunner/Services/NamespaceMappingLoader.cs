using System.Text.Json;

namespace PipelineRunner.Services;

/// <summary>
/// Loads namespace-to-filename mappings from config/namespace-mapping.json.
/// </summary>
public sealed class NamespaceMappingLoader : INamespaceMappingLoader
{
    private const string RelativePath = "config/namespace-mapping.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, string>> LoadAsync(string repoRoot, CancellationToken cancellationToken)
    {
        var filePath = Path.Combine(repoRoot, RelativePath);
        if (!File.Exists(filePath))
        {
            Console.Error.WriteLine($"Warning: Namespace mapping file not found at '{filePath}'. Returning empty mapping.");
            return new Dictionary<string, string>();
        }

        await using var stream = File.OpenRead(filePath);

        try
        {
            var mapping = await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(stream, JsonOptions, cancellationToken);
            return mapping ?? new Dictionary<string, string>();
        }
        catch (JsonException exception)
        {
            Console.Error.WriteLine($"Warning: Failed to parse namespace mapping file '{filePath}': {exception.Message}");
            return new Dictionary<string, string>();
        }
    }
}
