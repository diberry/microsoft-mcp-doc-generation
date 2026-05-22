using System.Globalization;
using System.Text.RegularExpressions;

namespace DocGeneration.Tools.Fingerprint;

/// <summary>
/// Scans generated-* directories and produces a FingerprintSnapshot.
/// </summary>
internal sealed class SnapshotGenerator
{
    private static readonly Regex TimestampedCatalogDirectoryPattern = new(
        @"^generated-(?<timestamp>\d{8}T\d{9}Z)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex TimestampedNamespaceDirectoryPattern = new(
        @"^generated-(?<namespace>.+)-(?<timestamp>\d{8}T\d{9}Z)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private readonly string _repoRoot;

    public SnapshotGenerator(string repoRoot)
    {
        _repoRoot = repoRoot;
    }

    /// <summary>
    /// Generates a snapshot for all generated-* directories under the repo root.
    /// </summary>
    public FingerprintSnapshot GenerateSnapshot(string? singleNamespace = null)
    {
        var snapshot = new FingerprintSnapshot
        {
            Timestamp = DateTime.UtcNow
        };

        var namespaceDirs = FindNamespaceDirectories(singleNamespace);
        foreach (var dir in namespaceDirs)
        {
            var nsName = ExtractNamespaceName(dir);
            snapshot.Namespaces[nsName] = GenerateNamespaceFingerprint(dir, nsName);
        }

        return snapshot;
    }

    internal IReadOnlyList<string> FindNamespaceDirectories(string? singleNamespace = null)
    {
        if (!Directory.Exists(_repoRoot))
            return [];

        var candidates = Directory.GetDirectories(_repoRoot, "generated-*")
            .Where(path => TryDescribeGeneratedNamespaceDirectory(path, out _));

        if (!string.IsNullOrWhiteSpace(singleNamespace))
        {
            candidates = candidates.Where(path => ExtractNamespaceName(path).Equals(singleNamespace, StringComparison.OrdinalIgnoreCase));
        }

        return candidates
            .GroupBy(ExtractNamespaceName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(GetDirectorySortKey)
                .ThenByDescending(Directory.GetLastWriteTimeUtc)
                .First())
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static string ExtractNamespaceName(string dirPath)
    {
        var dirName = Path.GetFileName(dirPath) ?? string.Empty;
        return TryDescribeGeneratedNamespaceDirectory(dirName, out var namespaceName)
            ? namespaceName
            : dirName;
    }

    private static bool TryDescribeGeneratedNamespaceDirectory(string dirPath, out string namespaceName)
    {
        namespaceName = string.Empty;

        var dirName = Path.GetFileName(dirPath) ?? string.Empty;
        if (dirName.Equals("generated", StringComparison.OrdinalIgnoreCase))
            return false;

        if (TimestampedCatalogDirectoryPattern.IsMatch(dirName))
            return false;

        var timestampedMatch = TimestampedNamespaceDirectoryPattern.Match(dirName);
        if (timestampedMatch.Success)
        {
            namespaceName = timestampedMatch.Groups["namespace"].Value;
            return true;
        }

        if (!dirName.StartsWith("generated-", StringComparison.OrdinalIgnoreCase))
            return false;

        namespaceName = dirName["generated-".Length..];
        return !string.IsNullOrWhiteSpace(namespaceName);
    }

    private static DateTimeOffset GetDirectorySortKey(string dirPath)
    {
        var dirName = Path.GetFileName(dirPath) ?? string.Empty;
        var timestampedMatch = TimestampedNamespaceDirectoryPattern.Match(dirName);
        if (timestampedMatch.Success && DateTimeOffset.TryParseExact(
                timestampedMatch.Groups["timestamp"].Value,
                "yyyyMMdd'T'HHmmssfff'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsedTimestamp))
        {
            return parsedTimestamp;
        }

        return Directory.GetLastWriteTimeUtc(dirPath);
    }

    internal NamespaceFingerprint GenerateNamespaceFingerprint(string nsDir, string nsName)
    {
        var allFiles = Directory.GetFiles(nsDir, "*", SearchOption.AllDirectories);
        var directories = new Dictionary<string, DirectoryFingerprint>();

        foreach (var subDir in Directory.GetDirectories(nsDir))
        {
            var subDirName = Path.GetFileName(subDir) ?? "";
            var subFiles = Directory.GetFiles(subDir, "*", SearchOption.AllDirectories);
            directories[subDirName] = new DirectoryFingerprint
            {
                FileCount = subFiles.Length,
                TotalSizeBytes = subFiles.Sum(f => new FileInfo(f).Length)
            };
        }

        // Find tool-family article
        string? toolFamilyContent = null;
        ArticleFingerprint? toolFamilyArticle = null;
        var toolFamilyDir = Path.Combine(nsDir, "tool-family");
        if (Directory.Exists(toolFamilyDir))
        {
            var mdFiles = Directory.GetFiles(toolFamilyDir, "*.md").OrderBy(f => f).ToArray();
            if (mdFiles.Length > 0)
            {
                toolFamilyContent = File.ReadAllText(mdFiles[0]);
                toolFamilyArticle = MarkdownAnalyzer.AnalyzeArticle(toolFamilyContent, Path.GetFileName(mdFiles[0]));
            }
        }

        // Find horizontal article
        ArticleFingerprint? horizontalArticle = null;
        var horizontalDir = Path.Combine(nsDir, "horizontal-articles");
        if (Directory.Exists(horizontalDir))
        {
            var mdFiles = Directory.GetFiles(horizontalDir, "*.md").OrderBy(f => f).ToArray();
            if (mdFiles.Length > 0)
            {
                var content = File.ReadAllText(mdFiles[0]);
                horizontalArticle = MarkdownAnalyzer.AnalyzeArticle(content, Path.GetFileName(mdFiles[0]));
            }
        }

        // Quality metrics from the cached tool-family content (no double read)
        QualityFingerprint? qualityMetrics = null;
        if (toolFamilyContent is not null)
        {
            qualityMetrics = MarkdownAnalyzer.AnalyzeQuality(toolFamilyContent);
        }

        return new NamespaceFingerprint
        {
            FileCount = allFiles.Length,
            TotalSizeBytes = allFiles.Sum(f => new FileInfo(f).Length),
            Directories = directories,
            ToolFamilyArticle = toolFamilyArticle,
            HorizontalArticle = horizontalArticle,
            QualityMetrics = qualityMetrics
        };
    }
}
