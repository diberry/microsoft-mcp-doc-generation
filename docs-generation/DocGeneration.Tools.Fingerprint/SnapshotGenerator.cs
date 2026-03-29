namespace DocGeneration.Tools.Fingerprint;

/// <summary>
/// Scans generated-* directories and produces a FingerprintSnapshot.
/// </summary>
internal sealed class SnapshotGenerator
{
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
        if (singleNamespace is not null)
        {
            var specific = Path.Combine(_repoRoot, $"generated-{singleNamespace}");
            return Directory.Exists(specific) ? [specific] : [];
        }

        return Directory.GetDirectories(_repoRoot, "generated-*")
            .Where(d => !Path.GetFileName(d).Equals("generated", StringComparison.OrdinalIgnoreCase))
            .OrderBy(d => d)
            .ToList();
    }

    internal static string ExtractNamespaceName(string dirPath)
    {
        var dirName = Path.GetFileName(dirPath) ?? "";
        return dirName.StartsWith("generated-", StringComparison.OrdinalIgnoreCase)
            ? dirName["generated-".Length..]
            : dirName;
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
        var toolFamilyDir = Path.Combine(nsDir, "tool-family");
        ArticleFingerprint? toolFamilyArticle = null;
        if (Directory.Exists(toolFamilyDir))
        {
            var mdFiles = Directory.GetFiles(toolFamilyDir, "*.md");
            if (mdFiles.Length > 0)
            {
                var mainArticle = mdFiles[0];
                var content = File.ReadAllText(mainArticle);
                toolFamilyArticle = MarkdownAnalyzer.AnalyzeArticle(content, Path.GetFileName(mainArticle));
            }
        }

        // Find horizontal article
        var horizontalDir = Path.Combine(nsDir, "horizontal-articles");
        ArticleFingerprint? horizontalArticle = null;
        if (Directory.Exists(horizontalDir))
        {
            var mdFiles = Directory.GetFiles(horizontalDir, "*.md");
            if (mdFiles.Length > 0)
            {
                var mainArticle = mdFiles[0];
                var content = File.ReadAllText(mainArticle);
                horizontalArticle = MarkdownAnalyzer.AnalyzeArticle(content, Path.GetFileName(mainArticle));
            }
        }

        // Quality metrics from the tool-family article (primary content)
        QualityFingerprint? qualityMetrics = null;
        if (toolFamilyArticle is not null)
        {
            var tfContent = File.ReadAllText(Path.Combine(toolFamilyDir!, Directory.GetFiles(toolFamilyDir!, "*.md")[0]));
            qualityMetrics = MarkdownAnalyzer.AnalyzeQuality(tfContent);
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
