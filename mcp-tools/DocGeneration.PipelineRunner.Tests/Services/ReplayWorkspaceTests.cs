using PipelineRunner.Services;
using Xunit;

namespace PipelineRunner.Tests.Unit;

public sealed class ReplayWorkspaceTests : IDisposable
{
    private readonly string _testRoot = Path.Combine(Path.GetTempPath(), $"pipeline-replay-workspace-{Guid.NewGuid():N}");

    public ReplayWorkspaceTests()
    {
        Directory.CreateDirectory(_testRoot);
    }

    [Fact]
    public void GetReplayWorkspace_WhenDirectoryExists_ReturnsResolvedPath()
    {
        var expectedPath = Path.Combine(_testRoot, "runs", "run-123");
        Directory.CreateDirectory(expectedPath);

        var result = WorkspaceManager.GetReplayWorkspace(_testRoot, "run-123");

        Assert.Equal(expectedPath, result);
    }

    [Fact]
    public void GetReplayWorkspace_WhenDirectoryMissing_ThrowsDirectoryNotFoundException()
    {
        var expectedPath = Path.Combine(_testRoot, "runs", "run-404");

        var exception = Assert.Throws<DirectoryNotFoundException>(() => WorkspaceManager.GetReplayWorkspace(_testRoot, "run-404"));

        Assert.Contains(expectedPath, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GetReplayWorkspace_CombinesRepoRootRunsAndRunId()
    {
        var expectedPath = Path.Combine(_testRoot, "runs", "run-xyz");
        Directory.CreateDirectory(expectedPath);

        var result = WorkspaceManager.GetReplayWorkspace(_testRoot, "run-xyz");

        Assert.Equal(Path.Combine(_testRoot, "runs", "run-xyz"), result);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, recursive: true);
        }
    }
}
