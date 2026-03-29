using System.Text.Json.Serialization;

namespace DocGeneration.Tools.Fingerprint;

/// <summary>
/// Root snapshot containing fingerprints for all namespaces.
/// Serialized to JSON for baseline storage and diff comparison.
/// </summary>
public sealed class FingerprintSnapshot
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0";

    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    [JsonPropertyName("namespaces")]
    public Dictionary<string, NamespaceFingerprint> Namespaces { get; init; } = new();
}

/// <summary>
/// Fingerprint data for a single namespace (e.g., generated-advisor/).
/// </summary>
public sealed class NamespaceFingerprint
{
    [JsonPropertyName("fileCount")]
    public int FileCount { get; init; }

    [JsonPropertyName("totalSizeBytes")]
    public long TotalSizeBytes { get; init; }

    [JsonPropertyName("directories")]
    public Dictionary<string, DirectoryFingerprint> Directories { get; init; } = new();

    [JsonPropertyName("toolFamilyArticle")]
    public ArticleFingerprint? ToolFamilyArticle { get; init; }

    [JsonPropertyName("horizontalArticle")]
    public ArticleFingerprint? HorizontalArticle { get; init; }

    [JsonPropertyName("qualityMetrics")]
    public QualityFingerprint? QualityMetrics { get; init; }
}

/// <summary>
/// Fingerprint for a single subdirectory within a namespace.
/// </summary>
public sealed class DirectoryFingerprint
{
    [JsonPropertyName("fileCount")]
    public int FileCount { get; init; }

    [JsonPropertyName("totalSizeBytes")]
    public long TotalSizeBytes { get; init; }
}

/// <summary>
/// Structural fingerprint for a key markdown article (tool-family or horizontal).
/// </summary>
public sealed class ArticleFingerprint
{
    [JsonPropertyName("fileName")]
    public string FileName { get; init; } = "";

    [JsonPropertyName("sizeBytes")]
    public long SizeBytes { get; init; }

    [JsonPropertyName("wordCount")]
    public int WordCount { get; init; }

    [JsonPropertyName("sectionCount")]
    public int SectionCount { get; init; }

    [JsonPropertyName("h2Headings")]
    public List<string> H2Headings { get; init; } = [];

    [JsonPropertyName("frontmatterFields")]
    public List<string> FrontmatterFields { get; init; } = [];

    [JsonPropertyName("toolCount")]
    public int? ToolCount { get; init; }
}

/// <summary>
/// Lightweight quality metrics for a namespace's key articles.
/// </summary>
public sealed class QualityFingerprint
{
    [JsonPropertyName("futureTenseViolations")]
    public int FutureTenseViolations { get; init; }

    [JsonPropertyName("fabricatedUrlCount")]
    public int FabricatedUrlCount { get; init; }

    [JsonPropertyName("brandingViolations")]
    public int BrandingViolations { get; init; }

    [JsonPropertyName("contractionRate")]
    public double ContractionRate { get; init; }
}
