namespace DocGeneration.PromptRegression.Tests.Infrastructure;

/// <summary>
/// Manages golden baseline files for prompt regression testing.
/// Baselines are stored in the test project's Baselines/ directory (committed to git).
/// Candidates are generated at test time in Candidates/ (gitignored).
/// </summary>
public sealed class BaselineManager
{
    private readonly string _baselinesRoot;
    private readonly string _candidatesRoot;

    public BaselineManager(string? projectRoot = null)
    {
        var root = projectRoot ?? FindProjectRoot();
        _baselinesRoot = Path.Combine(root, "Baselines");
        _candidatesRoot = Path.Combine(root, "Candidates");
    }

    public string BaselinesRoot => _baselinesRoot;
    public string CandidatesRoot => _candidatesRoot;

    /// <summary>
    /// Returns all namespaces that have baselines captured.
    /// </summary>
    public IReadOnlyList<string> GetBaselineNamespaces()
    {
        if (!Directory.Exists(_baselinesRoot)) return [];
        return Directory.GetDirectories(_baselinesRoot)
            .Select(Path.GetFileName)
            .Where(n => n is not null)
            .Cast<string>()
            .OrderBy(n => n)
            .ToList();
    }

    /// <summary>
    /// Gets the baseline file path for a specific namespace and file type.
    /// </summary>
    public string GetBaselinePath(string ns, string fileName) =>
        Path.Combine(_baselinesRoot, ns, fileName);

    /// <summary>
    /// Gets the candidate file path for a specific namespace and file type.
    /// </summary>
    public string GetCandidatePath(string ns, string fileName) =>
        Path.Combine(_candidatesRoot, ns, fileName);

    /// <summary>
    /// Reads baseline content if it exists, returns null otherwise.
    /// </summary>
    public string? ReadBaseline(string ns, string fileName)
    {
        var path = GetBaselinePath(ns, fileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>
    /// Reads candidate content if it exists, returns null otherwise.
    /// </summary>
    public string? ReadCandidate(string ns, string fileName)
    {
        var path = GetCandidatePath(ns, fileName);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    /// <summary>
    /// Saves content as a baseline golden file.
    /// </summary>
    public void SaveBaseline(string ns, string fileName, string content)
    {
        var path = GetBaselinePath(ns, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Saves content as a candidate file for comparison.
    /// </summary>
    public void SaveCandidate(string ns, string fileName, string content)
    {
        var path = GetCandidatePath(ns, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    /// <summary>
    /// Lists all baseline files for a namespace.
    /// </summary>
    public IReadOnlyList<string> ListBaselineFiles(string ns)
    {
        var dir = Path.Combine(_baselinesRoot, ns);
        if (!Directory.Exists(dir)) return [];
        return Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(dir, f))
            .OrderBy(f => f)
            .ToList();
    }

    /// <summary>
    /// Compares baseline and candidate metrics for a specific file.
    /// </summary>
    public MetricsComparison? Compare(string ns, string fileName)
    {
        var baseline = ReadBaseline(ns, fileName);
        var candidate = ReadCandidate(ns, fileName);
        if (baseline is null || candidate is null) return null;

        return new MetricsComparison
        {
            Namespace = ns,
            FileType = fileName,
            Baseline = QualityMetrics.Analyze(baseline),
            Candidate = QualityMetrics.Analyze(candidate),
        };
    }

    private static string FindProjectRoot()
    {
        return ProjectRootFinder.FindTestProjectRoot();
    }
}
