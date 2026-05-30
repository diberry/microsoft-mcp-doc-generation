using PipelineRunner.Contracts;

namespace PipelineRunner.Services;

public sealed class WorkspaceManager
{
    private readonly HashSet<string> _trackedDirectories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Resolves the workspace directory for a prior pipeline run.
    /// Run directories are expected at: {repoRoot}\runs\{runId}\
    /// </summary>
    public static string GetReplayWorkspace(string repoRoot, string runId)
    {
        var path = Path.Combine(repoRoot, "runs", runId);
        if (!Directory.Exists(path))
        {
            throw new DirectoryNotFoundException($"Replay run directory not found: {path}");
        }

        return path;
    }

    public string CreateTemporaryDirectory(string prefix)
    {
        var directoryPath = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directoryPath);
        _trackedDirectories.Add(directoryPath);
        return directoryPath;
    }

    public void Delete(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        _trackedDirectories.Remove(path);
    }

    public void DeleteAll()
    {
        foreach (var directory in _trackedDirectories.ToArray())
        {
            Delete(directory);
        }
    }

    /// <summary>
    /// Checks the step workspace for the 5-file observability contract.
    /// Returns list of missing file names (empty if all present).
    /// Does NOT throw — enforcement is at WARNING level.
    /// </summary>
    public IReadOnlyList<string> AssertOutputContract(StageOutputContract contract)
    {
        var missing = new List<string>();
        foreach (var expectedPath in contract.GetExpectedFiles())
        {
            if (!File.Exists(expectedPath))
            {
                missing.Add(Path.GetFileName(expectedPath));
            }
        }

        return missing;
    }
}
