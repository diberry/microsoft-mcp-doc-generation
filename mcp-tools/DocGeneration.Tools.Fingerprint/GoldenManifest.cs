using System.Text.Json.Serialization;

namespace DocGeneration.Tools.Fingerprint;

/// <summary>
/// Root golden snapshot manifest used by the behavioral equivalence CI gate.
/// </summary>
public sealed class GoldenManifest
{
    /// <summary>
    /// Gets the manifest schema version.
    /// </summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// Gets the namespace represented by the manifest.
    /// </summary>
    [JsonPropertyName("namespace")]
    public string Namespace { get; init; } = "";

    /// <summary>
    /// Gets the UTC capture timestamp.
    /// </summary>
    [JsonPropertyName("capturedAt")]
    public DateTime CapturedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets deterministic file entries keyed by normalized relative path.
    /// </summary>
    [JsonPropertyName("deterministicFiles")]
    public Dictionary<string, DeterministicFileEntry> DeterministicFiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets AI file entries keyed by normalized relative path.
    /// </summary>
    [JsonPropertyName("aiFiles")]
    public Dictionary<string, AiFileEntry> AiFiles { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Hash metadata for a deterministic file.
/// </summary>
public sealed class DeterministicFileEntry
{
    /// <summary>
    /// Gets the lowercase SHA-256 hash for the file contents.
    /// </summary>
    [JsonPropertyName("sha256")]
    public string Sha256 { get; init; } = "";

    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; init; }
}

/// <summary>
/// Structural metadata for an AI-generated file.
/// </summary>
public sealed class AiFileEntry
{
    /// <summary>
    /// Gets the file size in bytes.
    /// </summary>
    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; init; }

    /// <summary>
    /// Gets the number of logical sections in the file.
    /// </summary>
    [JsonPropertyName("sectionCount")]
    public int SectionCount { get; init; }

    /// <summary>
    /// Gets the required top-level keys that must remain present.
    /// </summary>
    [JsonPropertyName("requiredKeys")]
    public List<string> RequiredKeys { get; init; } = [];
}
