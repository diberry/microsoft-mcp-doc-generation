using System.Security.Cryptography;
using System.Text.Json;

namespace DocGeneration.Tools.Fingerprint;

/// <summary>
/// Captures golden manifests from generated namespace output directories.
/// </summary>
internal sealed class GoldenSnapshotCapture
{
    private static readonly HashSet<string> DeterministicDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "annotations",
        "parameters",
        "h2-headings",
        "cli",
        "reports",
        "logs",
        "common-general"
    };

    private static readonly HashSet<string> AiDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "tools",
        "tool-family",
        "horizontal-articles",
        "example-prompts",
        "e2e-test-prompts"
    };

    private readonly string _repoRoot;

    /// <summary>
    /// Initializes a new capture helper rooted at the repository root.
    /// </summary>
    /// <param name="repoRoot">Repository root used to locate generated directories.</param>
    public GoldenSnapshotCapture(string repoRoot)
    {
        _repoRoot = repoRoot;
    }

    /// <summary>
    /// Captures a golden manifest for the latest generated directory of the supplied namespace.
    /// </summary>
    /// <param name="namespaceName">Namespace to capture.</param>
    /// <returns>The generated golden manifest.</returns>
    public GoldenManifest Capture(string namespaceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceName);

        var generator = new SnapshotGenerator(_repoRoot);
        var outputDirectory = generator.FindNamespaceDirectories(namespaceName).FirstOrDefault();
        if (outputDirectory is null)
        {
            throw new DirectoryNotFoundException(
                $"Could not find a generated directory for namespace '{namespaceName}' under '{_repoRoot}'.");
        }

        return CaptureFromOutputDirectory(outputDirectory, namespaceName);
    }

    /// <summary>
    /// Captures a golden manifest from a specific output directory.
    /// </summary>
    /// <param name="outputDirectory">Generated namespace output directory.</param>
    /// <param name="namespaceName">Namespace represented by the directory.</param>
    /// <returns>The generated golden manifest.</returns>
    internal GoldenManifest CaptureFromOutputDirectory(string outputDirectory, string namespaceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(namespaceName);

        if (!Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException($"Output directory not found: {outputDirectory}");
        }

        var manifest = new GoldenManifest
        {
            Namespace = namespaceName.Trim(),
            CapturedAt = DateTime.UtcNow
        };

        foreach (var filePath in Directory.GetFiles(outputDirectory, "*", SearchOption.AllDirectories)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var relativePath = NormalizeRelativePath(Path.GetRelativePath(outputDirectory, filePath));
            var classification = Classify(relativePath);

            switch (classification)
            {
                case GoldenFileKind.Deterministic:
                    manifest.DeterministicFiles[relativePath] = CaptureDeterministicFile(filePath);
                    break;
                case GoldenFileKind.Ai:
                    manifest.AiFiles[relativePath] = CaptureAiFile(filePath);
                    break;
                case GoldenFileKind.Ignore:
                    break;
            }
        }

        return manifest;
    }

    private static GoldenFileKind Classify(string relativePath)
    {
        var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return GoldenFileKind.Ignore;
        }

        if (segments.Length == 1)
        {
            return GoldenFileKind.Deterministic;
        }

        var topLevelDirectory = segments[0];
        if (DeterministicDirectories.Contains(topLevelDirectory))
        {
            return GoldenFileKind.Deterministic;
        }

        if (AiDirectories.Contains(topLevelDirectory))
        {
            return GoldenFileKind.Ai;
        }

        return GoldenFileKind.Ignore;
    }

    private static DeterministicFileEntry CaptureDeterministicFile(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        var hash = SHA256.HashData(stream);

        return new DeterministicFileEntry
        {
            Sha256 = Convert.ToHexString(hash).ToLowerInvariant(),
            SizeBytes = new FileInfo(filePath).Length
        };
    }

    private static AiFileEntry CaptureAiFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var fileInfo = new FileInfo(filePath);
        var extension = Path.GetExtension(filePath);

        var (sectionCount, requiredKeys) = extension.ToLowerInvariant() switch
        {
            ".md" => ExtractMarkdownStructure(content, filePath),
            ".json" => ExtractJsonStructure(content),
            _ => ExtractTextStructure(content)
        };

        return new AiFileEntry
        {
            SizeBytes = fileInfo.Length,
            SectionCount = sectionCount,
            RequiredKeys = requiredKeys
        };
    }

    private static (int SectionCount, List<string> RequiredKeys) ExtractMarkdownStructure(string content, string filePath)
    {
        var article = MarkdownAnalyzer.AnalyzeArticle(content, Path.GetFileName(filePath));
        return (
            article.H2Headings.Count,
            article.FrontmatterFields
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static (int SectionCount, List<string> RequiredKeys) ExtractJsonStructure(string content)
    {
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;

        var requiredKeys = new List<string>();
        var sectionCount = 0;

        if (root.ValueKind == JsonValueKind.Object)
        {
            requiredKeys.AddRange(root.EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase));

            if (root.TryGetProperty("sections", out var sectionsElement) && sectionsElement.ValueKind == JsonValueKind.Array)
            {
                sectionCount = sectionsElement.GetArrayLength();
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            sectionCount = root.GetArrayLength();
        }

        return (sectionCount, requiredKeys);
    }

    private static (int SectionCount, List<string> RequiredKeys) ExtractTextStructure(string content)
        => (MarkdownAnalyzer.ExtractH2Headings(content).Count, []);

    private static string NormalizeRelativePath(string relativePath)
        => relativePath.Replace('\\', '/');

    /// <summary>
    /// Represents how a file should be evaluated by the golden gate.
    /// </summary>
    private enum GoldenFileKind
    {
        Ignore,
        Deterministic,
        Ai
    }
}
