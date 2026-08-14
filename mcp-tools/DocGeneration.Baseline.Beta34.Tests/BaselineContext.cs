using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DocGeneration.Baseline.Beta34.Tests;

/// <summary>
/// Deterministic access layer for the frozen beta.34 baseline. Tests read ONLY the committed
/// fixtures + manifest, except the source-integrity / duplicate-accounting tests which read the
/// in-repo source run directory READ-ONLY to prove capture integrity. No test invokes the
/// pipeline, MCP, Azure OpenAI, or the network.
/// </summary>
internal static class BaselineContext
{
    public const int ExpectedRecordCount = 34;
    public const string ExpectedAzureMcpBuild = "3.0.0-beta.34+eec7acccddab1e16be852a3c3b9503cc9adf7538";
    public const string ExpectedSourceRunDir = "generated-20260813T162453";

    // The exhaustive set of placeholder tokens the sanitizer is allowed to emit. This MUST match the
    // eight tokens defined by the sanitization contract in `scripts/baseline/New-Beta34Baseline.ps1`
    // and documented in `scripts/baseline/README.md` (AD-028). Of these, only <REPO>, <TEMP>,
    // <RUNSTAMP>, and <GUID> actually occur in the current beta.34 fixtures; the remaining four
    // (<USER>, <USER_HOME>, <HOST>, <PATH>) are defensive rules that may not appear but are still
    // permitted output of the sanitizer. Keeping this list aligned with the script is required by
    // AD-010 so T22 does not become a false positive against a correctly-sanitized baseline.
    public static readonly HashSet<string> ApprovedPlaceholders =
        new(StringComparer.Ordinal)
        {
            "<REPO>", "<TEMP>", "<USER>", "<USER_HOME>", "<HOST>", "<RUNSTAMP>", "<GUID>", "<PATH>",
        };

    public static readonly string[] ValidClassifications = { "root", "cascade", "mixed", "diagnostic" };
    public static readonly string[] ValidErrorClasses = { "A", "B", "A+B", "C" };
    public static readonly string[] ValidChainRoles = { "root", "cascade" };

    private static string? _repoRoot;

    public static string RepoRoot => _repoRoot ??= FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "mcp-doc-generation.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Could not locate repo root (mcp-doc-generation.sln) walking up from " + AppContext.BaseDirectory);
    }

    public static string ProjectDir =>
        Path.Combine(RepoRoot, "mcp-tools", "DocGeneration.Baseline.Beta34.Tests");

    /// <summary>
    /// Root of the frozen fixtures. Defaults to the committed project Fixtures directory, but can be
    /// overridden via the BETA34_FIXTURES_DIR environment variable. The override exists so the
    /// RED-first (baseline-absent) evidence can be reproduced by pointing at a non-existent directory
    /// WITHOUT disturbing the shared working tree — it is not used by CI.
    /// </summary>
    public static string FixturesDir =>
        Environment.GetEnvironmentVariable("BETA34_FIXTURES_DIR") is { Length: > 0 } overrideDir
            ? overrideDir
            : Path.Combine(ProjectDir, "Fixtures");

    public static string FixtureCriticalFailuresDir => Path.Combine(FixturesDir, "critical-failures");

    public static string ManifestPath => Path.Combine(FixturesDir, "beta34-baseline-manifest.json");

    /// <summary>
    /// Committed capture-inventory fixture produced by Quinn's generator. It records the 68 physical
    /// copies (34 catalog + 34 namespace) of the source run with per-copy sha256, logical identity,
    /// and stable id, so the immutability / duplicate-accounting tests can run on a CLEAN CHECKOUT
    /// without the gitignored source run directory (Cameron BLOCKING-1 / Ellis blocking-2).
    /// </summary>
    public static string SourceInventoryPath => Path.Combine(FixturesDir, "source-inventory.json");

    public static string SourceRunDir => Path.Combine(RepoRoot, ExpectedSourceRunDir);

    public static string SourceCriticalFailuresDir => Path.Combine(SourceRunDir, "critical-failures");

    /// <summary>Committed fixture files (empty array if the frozen fixtures do not exist yet — RED).</summary>
    public static string[] FixtureFiles() =>
        Directory.Exists(FixtureCriticalFailuresDir)
            ? Directory.GetFiles(FixtureCriticalFailuresDir, "*.json")
                .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

    /// <summary>Source catalog critical-failure files (read-only capture integrity).</summary>
    public static string[] SourceCatalogFiles() =>
        Directory.Exists(SourceCriticalFailuresDir)
            ? Directory.GetFiles(SourceCriticalFailuresDir, "*.json")
                .OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();

    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Loads the frozen manifest. Throws FileNotFoundException when the manifest has not been
    /// produced yet — an intentional RED failure prior to Quinn's generator running.
    /// </summary>
    public static Manifest LoadManifest()
    {
        if (!File.Exists(ManifestPath))
        {
            throw new FileNotFoundException(
                $"Frozen baseline manifest not found (expected committed fixture). Path: {ManifestPath}",
                ManifestPath);
        }

        byte[] bytes = File.ReadAllBytes(ManifestPath);
        Manifest? manifest = JsonSerializer.Deserialize<Manifest>(bytes, ManifestJsonOptions);
        if (manifest is null)
        {
            throw new InvalidOperationException("Manifest deserialized to null: " + ManifestPath);
        }
        return manifest;
    }

    /// <summary>
    /// Loads the committed source inventory. Throws FileNotFoundException when it has not been
    /// produced yet — an intentional RED failure prior to Quinn's generator running.
    /// </summary>
    public static SourceInventory LoadSourceInventory()
    {
        if (!File.Exists(SourceInventoryPath))
        {
            throw new FileNotFoundException(
                $"Frozen source inventory not found (expected committed fixture). Path: {SourceInventoryPath}",
                SourceInventoryPath);
        }

        byte[] bytes = File.ReadAllBytes(SourceInventoryPath);
        SourceInventory? inventory = JsonSerializer.Deserialize<SourceInventory>(bytes, ManifestJsonOptions);
        if (inventory is null)
        {
            throw new InvalidOperationException("Source inventory deserialized to null: " + SourceInventoryPath);
        }
        return inventory;
    }

    public static string Sha256Hex(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>
    /// Resolves a manifest-recorded source/physical path against the SOURCE RUN directory.
    /// Quinn's manifest records source paths relative to the run root (e.g. "critical-failures/..").
    /// </summary>
    public static string ResolveSourceRelative(string relative)
    {
        string normalized = relative.Replace('\\', '/').TrimStart('.', '/');
        return Path.GetFullPath(Path.Combine(SourceRunDir, normalized.Replace('/', Path.DirectorySeparatorChar)));
    }

    /// <summary>
    /// Resolves a source-inventory <c>relativePath</c> (recorded relative to the REPO ROOT, e.g.
    /// "generated-20260813T162453/appconfig/critical-failures/…") against the working tree. Used only
    /// by the opt-in deep source-run verification, which requires the (gitignored) source run present.
    /// </summary>
    public static string ResolveRepoRelative(string relative)
    {
        string normalized = relative.Replace('\\', '/').TrimStart('/');
        return Path.GetFullPath(Path.Combine(RepoRoot, normalized.Replace('/', Path.DirectorySeparatorChar)));
    }

    /// <summary>Parses JSON bytes, tolerating a leading UTF-8 BOM (present in the raw source captures).</summary>
    public static JsonDocument ParseJson(byte[] bytes)
    {
        int offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        return JsonDocument.Parse(bytes.AsMemory(offset));
    }

    /// <summary>Parses the logical identity 4-tuple (namespace|stepId|artifactName|recordedAtUtc) from a record file.</summary>
    public static string LogicalIdentity(string filePath)
    {
        byte[] bytes = File.ReadAllBytes(filePath);
        using JsonDocument doc = ParseJson(bytes);
        JsonElement root = doc.RootElement;
        string ns = root.GetProperty("namespace").GetString() ?? "";
        int step = root.GetProperty("stepId").GetInt32();
        string artifact = root.GetProperty("artifactName").GetString() ?? "";
        string recordedAt = root.GetProperty("recordedAtUtc").GetString() ?? "";
        return string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{ns}|{step}|{artifact}|{recordedAt}");
    }
}

internal static class StableIdDeriver
{
    public static string Kebab(string s)
    {
        s = s.Trim().ToLowerInvariant();
        var sb = new StringBuilder(s.Length);
        bool prevDash = false;
        foreach (char ch in s)
        {
            if ((ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9'))
            {
                sb.Append(ch);
                prevDash = false;
            }
            else if (!prevDash)
            {
                sb.Append('-');
                prevDash = true;
            }
        }
        return sb.ToString().Trim('-');
    }

    public static string Slug(string ns, string artifactName)
    {
        string[] tokens = artifactName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length > 1 && tokens[0].Equals(ns, StringComparison.OrdinalIgnoreCase))
        {
            tokens = tokens[1..];
        }
        return Kebab(string.Join('-', tokens));
    }

    /// <summary>The path/timestamp-independent stable-id prefix: {namespace}.{stepId:D2}.{artifactSlug}.</summary>
    public static string Prefix(string ns, int stepId, string artifactName) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture,
            $"{ns}.{stepId:D2}.{Slug(ns, artifactName)}");
}

internal sealed record Manifest
{
    [JsonPropertyName("schemaVersion")] public JsonElement SchemaVersion { get; init; }
    [JsonPropertyName("provenance")] public Provenance? Provenance { get; init; }
    [JsonPropertyName("accounting")] public Accounting? Accounting { get; init; }
    [JsonPropertyName("records")] public List<BaselineRecord> Records { get; init; } = new();
}

internal sealed record Accounting
{
    [JsonPropertyName("logicalRecords")] public int LogicalRecords { get; init; }
    [JsonPropertyName("physicalCopies")] public int PhysicalCopies { get; init; }
    [JsonPropertyName("step2Records")] public int Step2Records { get; init; }
    [JsonPropertyName("step4Records")] public int Step4Records { get; init; }
    [JsonPropertyName("dependentRecords")] public int DependentRecords { get; init; }
    [JsonPropertyName("dependencyLinks")] public int DependencyLinks { get; init; }
    [JsonPropertyName("chainRoleCounts")] public Dictionary<string, int> ChainRoleCounts { get; init; } = new();
    [JsonPropertyName("classificationCounts")] public Dictionary<string, int> ClassificationCounts { get; init; } = new();
    [JsonPropertyName("errorClassCounts")] public Dictionary<string, int> ErrorClassCounts { get; init; } = new();
}

internal sealed record Provenance
{
    [JsonPropertyName("repoCommitSha")] public string? RepoCommitSha { get; init; }
    [JsonPropertyName("sourceRunDir")] public string? SourceRunDir { get; init; }
    [JsonPropertyName("azureMcpBuild")] public string? AzureMcpBuild { get; init; }
    [JsonPropertyName("sanitizerVersion")] public string? SanitizerVersion { get; init; }
    [JsonPropertyName("captureTimestampUtc")] public string? CaptureTimestampUtc { get; init; }
    [JsonPropertyName("ai")] public JsonElement Ai { get; init; }
    [JsonPropertyName("configHashes")] public JsonElement ConfigHashes { get; init; }
    [JsonPropertyName("promptHashes")] public JsonElement PromptHashes { get; init; }
    [JsonPropertyName("toolVersions")] public JsonElement ToolVersions { get; init; }
}

internal sealed record BaselineRecord
{
    [JsonPropertyName("stableId")] public string? StableId { get; init; }
    [JsonPropertyName("namespace")] public string? Namespace { get; init; }
    [JsonPropertyName("stepId")] public int StepId { get; init; }
    [JsonPropertyName("artifactName")] public string? ArtifactName { get; init; }
    [JsonPropertyName("recordedAtUtc")] public string? RecordedAtUtc { get; init; }
    [JsonPropertyName("sourceRelativePath")] public string? SourceRelativePath { get; init; }
    [JsonPropertyName("sourceSha256")] public string? SourceSha256 { get; init; }
    [JsonPropertyName("sanitizedRelativePath")] public string? SanitizedRelativePath { get; init; }
    [JsonPropertyName("sanitizedSha256")] public string? SanitizedSha256 { get; init; }
    [JsonPropertyName("classification")] public string? Classification { get; init; }
    [JsonPropertyName("errorClass")] public string? ErrorClass { get; init; }
    [JsonPropertyName("errorClasses")] public List<string> ErrorClasses { get; init; } = new();
    [JsonPropertyName("hasUpstreamStep2")] public bool HasUpstreamStep2 { get; init; }
    [JsonPropertyName("chainRole")] public string? ChainRole { get; init; }
    [JsonPropertyName("upstreamStableIds")] public List<string> UpstreamStableIds { get; init; } = new();
    [JsonPropertyName("physicalCopies")] public List<string> PhysicalCopies { get; init; } = new();
    [JsonPropertyName("rationale")] public string? Rationale { get; init; }

    /// <summary>The committed fixture filename linked to this record (basename of sanitizedRelativePath).</summary>
    public string FixtureFileName =>
        Path.GetFileName((SanitizedRelativePath ?? string.Empty).Replace('\\', '/'));
}

internal sealed record SourceInventory
{
    [JsonPropertyName("schemaVersion")] public string? SchemaVersion { get; init; }
    [JsonPropertyName("sourceRunDir")] public string? SourceRunDir { get; init; }
    [JsonPropertyName("generatedAtUtc")] public string? GeneratedAtUtc { get; init; }
    [JsonPropertyName("physicalCopyCount")] public int PhysicalCopyCount { get; init; }
    [JsonPropertyName("logicalRecordCount")] public int LogicalRecordCount { get; init; }
    [JsonPropertyName("physicalCopies")] public List<InventoryCopy> PhysicalCopies { get; init; } = new();
}

internal sealed record InventoryCopy
{
    [JsonPropertyName("relativePath")] public string? RelativePath { get; init; }
    [JsonPropertyName("copyKind")] public string? CopyKind { get; init; }
    [JsonPropertyName("sha256")] public string? Sha256 { get; init; }
    [JsonPropertyName("logicalIdentity")] public string? LogicalIdentity { get; init; }
    [JsonPropertyName("stableId")] public string? StableId { get; init; }
}
