using DocGeneration.PromptRegression.Tests.Infrastructure;

namespace DocGeneration.PromptRegression.Tests.Tests;

/// <summary>
/// Tests for BaselineManager file operations.
/// Uses a temporary directory to avoid polluting the real Baselines/ directory.
/// </summary>
public class BaselineManagerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly BaselineManager _manager;

    public BaselineManagerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"prompt-regression-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        // Create subdirectories that BaselineManager expects
        Directory.CreateDirectory(Path.Combine(_tempDir, "Baselines"));
        Directory.CreateDirectory(Path.Combine(_tempDir, "Candidates"));
        _manager = new BaselineManager(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void GetBaselineNamespaces_Empty_ReturnsEmpty()
    {
        var namespaces = _manager.GetBaselineNamespaces();
        Assert.Empty(namespaces);
    }

    [Fact]
    public void SaveBaseline_CreatesFile()
    {
        _manager.SaveBaseline("advisor", "tool-family.md", "# Test content");

        var content = _manager.ReadBaseline("advisor", "tool-family.md");
        Assert.Equal("# Test content", content);
    }

    [Fact]
    public void GetBaselineNamespaces_AfterSave_ReturnsNamespace()
    {
        _manager.SaveBaseline("advisor", "tool-family.md", "content");
        _manager.SaveBaseline("storage", "tool-family.md", "content");

        var namespaces = _manager.GetBaselineNamespaces();
        Assert.Equal(2, namespaces.Count);
        Assert.Contains("advisor", namespaces);
        Assert.Contains("storage", namespaces);
    }

    [Fact]
    public void ReadBaseline_NonExistent_ReturnsNull()
    {
        var content = _manager.ReadBaseline("nonexistent", "file.md");
        Assert.Null(content);
    }

    [Fact]
    public void SaveCandidate_CreatesFile()
    {
        _manager.SaveCandidate("advisor", "tool-family.md", "# Candidate content");

        var content = _manager.ReadCandidate("advisor", "tool-family.md");
        Assert.Equal("# Candidate content", content);
    }

    [Fact]
    public void ListBaselineFiles_ReturnsAllMdFiles()
    {
        _manager.SaveBaseline("storage", "tool-family.md", "content1");
        _manager.SaveBaseline("storage", "horizontal-article.md", "content2");

        var files = _manager.ListBaselineFiles("storage");
        Assert.Equal(2, files.Count);
        Assert.Contains("tool-family.md", files);
        Assert.Contains("horizontal-article.md", files);
    }

    [Fact]
    public void Compare_BothExist_ReturnsComparison()
    {
        var baseline = """
            ---
            title: Test
            ms.topic: concept-article
            ms.date: 03/27/2026
            ---

            # Test

            ## Prerequisites

            Content.

            ## Best practices

            Do not hardcode credentials.

            ## Related content

            - [Link](/link)
            """;

        var candidate = """
            ---
            title: Test
            ms.topic: concept-article
            ms.date: 03/27/2026
            ---

            # Test

            ## Prerequisites

            Content.

            ## Best practices

            Don't hardcode credentials. It's more secure.

            ## Related content

            - [Link](/link)
            """;

        _manager.SaveBaseline("advisor", "article.md", baseline);
        _manager.SaveCandidate("advisor", "article.md", candidate);

        var comparison = _manager.Compare("advisor", "article.md");

        Assert.NotNull(comparison);
        Assert.Equal("advisor", comparison.Namespace);
        Assert.True(comparison.Candidate.ContractionCount > comparison.Baseline.ContractionCount);
    }

    [Fact]
    public void Compare_MissingCandidate_ReturnsNull()
    {
        _manager.SaveBaseline("advisor", "article.md", "content");

        var comparison = _manager.Compare("advisor", "article.md");
        Assert.Null(comparison);
    }
}
