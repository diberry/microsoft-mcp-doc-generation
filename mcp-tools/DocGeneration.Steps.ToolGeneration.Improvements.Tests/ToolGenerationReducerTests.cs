// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using ToolGeneration_Improved.Services;

namespace DocGeneration.Steps.ToolGeneration.Improvements.Tests;

public sealed class ToolGenerationReducerTests : IDisposable
{
    private readonly string _testRoot;

    public ToolGenerationReducerTests()
    {
        _testRoot = Path.Combine(AppContext.BaseDirectory, "tool-generation-reducer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testRoot))
        {
            Directory.Delete(_testRoot, true);
        }
    }

    [Fact]
    public async Task ReduceAsync_ReadsComposedFile_AndBuildsContext()
    {
        var composedDir = Path.Combine(_testRoot, "composed");
        Directory.CreateDirectory(composedDir);

        const string fileName = "fileshare-create.md";
        const string content = """
            # create

            <!-- @mcpcli fileshares fileshare create -->

            Tool content.
            """;
        await File.WriteAllTextAsync(Path.Combine(composedDir, fileName), content);

        var reducer = new ToolGenerationReducer();

        var result = await reducer.ReduceAsync(composedDir, fileName, 4096, CancellationToken.None);

        Assert.Equal("fileshares fileshare create", result.ToolName);
        Assert.Equal(content.ReplaceLineEndings(), result.ComposedContent.ReplaceLineEndings());
        Assert.Equal(4096, result.MaxTokens);
        Assert.Equal("1.0", result.SchemaVersion);
    }

    [Fact]
    public async Task ReduceAsync_MissingFile_ThrowsFileNotFoundException()
    {
        var composedDir = Path.Combine(_testRoot, "missing");
        Directory.CreateDirectory(composedDir);

        var reducer = new ToolGenerationReducer();

        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            reducer.ReduceAsync(composedDir, "missing-tool.md", 1024, CancellationToken.None));

        Assert.EndsWith("missing-tool.md", ex.FileName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReduceAsync_SchemaVersion_IsAlwaysOnePointZero()
    {
        var composedDir = Path.Combine(_testRoot, "schema");
        Directory.CreateDirectory(composedDir);

        const string fileName = "tool.md";
        await File.WriteAllTextAsync(Path.Combine(composedDir, fileName), "# tool");

        var reducer = new ToolGenerationReducer();

        var result = await reducer.ReduceAsync(composedDir, fileName, 123, CancellationToken.None);

        Assert.Equal("1.0", result.SchemaVersion);
    }
}
