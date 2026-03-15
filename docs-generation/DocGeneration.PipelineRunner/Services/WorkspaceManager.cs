namespace PipelineRunner.Services;

public sealed class WorkspaceManager : IWorkspaceManager
{
    private readonly HashSet<string> _trackedDirectories = new(StringComparer.OrdinalIgnoreCase);

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
}
