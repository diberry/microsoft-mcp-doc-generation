// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolFamilyCleanup.Services;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests;

/// <summary>
/// Failure path tests for ToolReader - validates graceful handling of
/// malformed inputs, missing markers, and empty directories.
/// Fixes: PR #519 review (Statler) - missing failure path coverage.
/// </summary>
public class ToolReaderFailurePathTests : IDisposable
{
    // NOTE: xUnit calls Dispose() even when a test throws, ensuring temp cleanup.
    // This is guaranteed by the xUnit framework's test lifecycle.
    private readonly string _tempDir;

    public ToolReaderFailurePathTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"toolreader-fail-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ReadAndGroupToolsAsync_EmptyDirectory_ReturnsEmptyDictionary()
    {
        var reader = new ToolReader(_tempDir);
        var result = await reader.ReadAndGroupToolsAsync();
        Assert.NotNull(result);
        Assert.Empty(result);
    }

    [Fact]
    public async Task ReadAndGroupToolsAsync_MissingH1Heading_ParsesWithFallbackToolName()
    {
        var malformedContent = "<!-- @mcpcli compute list -->\n\nLists all virtual machines.\n";
        File.WriteAllText(Path.Combine(_tempDir, "compute-list.md"), malformedContent);

        var reader = new ToolReader(_tempDir);
        var result = await reader.ReadAndGroupToolsAsync();

        Assert.Single(result);
        Assert.True(result.ContainsKey("compute"));
        var tools = result["compute"];
        Assert.Single(tools);
        Assert.Equal("Unknown Tool", tools[0].ToolName);
        Assert.Equal("compute list", tools[0].Command);
    }

    [Fact]
    public async Task ReadAndGroupToolsAsync_MissingMcpCliComment_DerivesFamilyFromFilename()
    {
        var noCommandContent = "# List storage accounts\n\nLists all storage accounts.\n";
        File.WriteAllText(Path.Combine(_tempDir, "storage-account-list.md"), noCommandContent);

        var reader = new ToolReader(_tempDir);
        var result = await reader.ReadAndGroupToolsAsync();

        Assert.Single(result);
        var familyName = result.Keys.First();
        Assert.NotEmpty(familyName);
        var tools = result[familyName];
        Assert.Single(tools);
        Assert.Equal("List storage accounts", tools[0].ToolName);
        Assert.Null(tools[0].Command);
    }

    [Fact]
    public async Task ReadAndGroupToolsAsync_NonExistentDirectory_ThrowsDirectoryNotFound()
    {
        var reader = new ToolReader(Path.Combine(_tempDir, "does-not-exist"));
        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => reader.ReadAndGroupToolsAsync());
    }
}