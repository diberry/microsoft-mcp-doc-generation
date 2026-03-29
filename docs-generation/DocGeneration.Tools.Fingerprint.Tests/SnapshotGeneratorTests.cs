namespace DocGeneration.Tools.Fingerprint.Tests;

public class SnapshotGeneratorTests
{
    [Fact]
    public void ExtractNamespaceName_RemovesPrefix()
    {
        Assert.Equal("advisor", SnapshotGenerator.ExtractNamespaceName("/repo/generated-advisor"));
        Assert.Equal("storage", SnapshotGenerator.ExtractNamespaceName(Path.Combine("C:", "repo", "generated-storage")));
        Assert.Equal("appservice", SnapshotGenerator.ExtractNamespaceName("generated-appservice"));
    }

    [Fact]
    public void ExtractNamespaceName_NoPrefix_ReturnsFullName()
    {
        Assert.Equal("some-dir", SnapshotGenerator.ExtractNamespaceName("/repo/some-dir"));
    }

    [Fact]
    public void FindNamespaceDirectories_SingleNamespace_FiltersCorrectly()
    {
        var tempDir = CreateTempRepoStructure();
        try
        {
            var generator = new SnapshotGenerator(tempDir);
            var dirs = generator.FindNamespaceDirectories("advisor");

            Assert.Single(dirs);
            Assert.EndsWith("generated-advisor", dirs[0].Replace('\\', '/'));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindNamespaceDirectories_AllNamespaces_FindsAll()
    {
        var tempDir = CreateTempRepoStructure();
        try
        {
            var generator = new SnapshotGenerator(tempDir);
            var dirs = generator.FindNamespaceDirectories();

            Assert.Equal(2, dirs.Count);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void FindNamespaceDirectories_NonexistentNamespace_ReturnsEmpty()
    {
        var tempDir = CreateTempRepoStructure();
        try
        {
            var generator = new SnapshotGenerator(tempDir);
            var dirs = generator.FindNamespaceDirectories("nonexistent");

            Assert.Empty(dirs);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GenerateSnapshot_CreatesNamespaceEntries()
    {
        var tempDir = CreateTempRepoStructure();
        try
        {
            var generator = new SnapshotGenerator(tempDir);
            var snapshot = generator.GenerateSnapshot();

            Assert.Equal(2, snapshot.Namespaces.Count);
            Assert.True(snapshot.Namespaces.ContainsKey("advisor"));
            Assert.True(snapshot.Namespaces.ContainsKey("storage"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GenerateSnapshot_CountsFilesCorrectly()
    {
        var tempDir = CreateTempRepoStructure();
        try
        {
            var generator = new SnapshotGenerator(tempDir);
            var snapshot = generator.GenerateSnapshot();

            // advisor has 3 files (1 in tool-family, 1 in annotations, 1 in horizontal-articles)
            Assert.Equal(3, snapshot.Namespaces["advisor"].FileCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GenerateSnapshot_TracksDirectories()
    {
        var tempDir = CreateTempRepoStructure();
        try
        {
            var generator = new SnapshotGenerator(tempDir);
            var snapshot = generator.GenerateSnapshot();

            Assert.True(snapshot.Namespaces["advisor"].Directories.ContainsKey("tool-family"));
            Assert.True(snapshot.Namespaces["advisor"].Directories.ContainsKey("annotations"));
            Assert.True(snapshot.Namespaces["advisor"].Directories.ContainsKey("horizontal-articles"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GenerateSnapshot_AnalyzesToolFamilyArticle()
    {
        var tempDir = CreateTempRepoStructure();
        try
        {
            var generator = new SnapshotGenerator(tempDir);
            var snapshot = generator.GenerateSnapshot();

            var tfArticle = snapshot.Namespaces["advisor"].ToolFamilyArticle;
            Assert.NotNull(tfArticle);
            Assert.Equal("advisor.md", tfArticle.FileName);
            Assert.Contains("## Get recommendations", tfArticle.H2Headings);
            Assert.Contains("title", tfArticle.FrontmatterFields);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GenerateSnapshot_ExtractsQualityMetrics()
    {
        var tempDir = CreateTempRepoStructure();
        try
        {
            var generator = new SnapshotGenerator(tempDir);
            var snapshot = generator.GenerateSnapshot();

            var qm = snapshot.Namespaces["advisor"].QualityMetrics;
            Assert.NotNull(qm);
            Assert.Equal(0, qm.FutureTenseViolations);
            Assert.Equal(0, qm.FabricatedUrlCount);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GenerateSnapshot_SingleNamespace_OnlyIncludesSpecified()
    {
        var tempDir = CreateTempRepoStructure();
        try
        {
            var generator = new SnapshotGenerator(tempDir);
            var snapshot = generator.GenerateSnapshot("advisor");

            Assert.Single(snapshot.Namespaces);
            Assert.True(snapshot.Namespaces.ContainsKey("advisor"));
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GenerateSnapshot_SetsVersionAndTimestamp()
    {
        var tempDir = CreateTempRepoStructure();
        try
        {
            var generator = new SnapshotGenerator(tempDir);
            var snapshot = generator.GenerateSnapshot();

            Assert.Equal("1.0", snapshot.Version);
            Assert.True(snapshot.Timestamp > DateTime.MinValue);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GenerateSnapshot_EmptyRepo_ReturnsEmptyNamespaces()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"fingerprint-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var generator = new SnapshotGenerator(tempDir);
            var snapshot = generator.GenerateSnapshot();

            Assert.Empty(snapshot.Namespaces);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GenerateSnapshot_AnalyzesHorizontalArticle()
    {
        var tempDir = CreateTempRepoStructure();
        try
        {
            var generator = new SnapshotGenerator(tempDir);
            var snapshot = generator.GenerateSnapshot();

            var ha = snapshot.Namespaces["advisor"].HorizontalArticle;
            Assert.NotNull(ha);
            Assert.Equal("horizontal-article-advisor.md", ha.FileName);
            Assert.Contains("## Prerequisites", ha.H2Headings);
            Assert.Contains("## Best practices", ha.H2Headings);
            Assert.Contains("title", ha.FrontmatterFields);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    // --- Helpers ---

    private static string CreateTempRepoStructure()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"fingerprint-test-{Guid.NewGuid():N}");

        // generated-advisor
        var advisorTfDir = Path.Combine(tempDir, "generated-advisor", "tool-family");
        Directory.CreateDirectory(advisorTfDir);
        File.WriteAllText(Path.Combine(advisorTfDir, "advisor.md"), """
            ---
            title: Azure MCP Server tools for Azure Advisor
            description: Advisor tools for recommendations.
            ms.date: 03/27/2026
            tool_count: 2
            ---

            # Azure MCP Server tools for Azure Advisor

            Advisor provides recommendations.

            ## Get recommendations

            This tool gets recommendations.

            ## Related content

            - [Advisor docs](/azure/advisor/)
            """);

        var advisorAnnDir = Path.Combine(tempDir, "generated-advisor", "annotations");
        Directory.CreateDirectory(advisorAnnDir);
        File.WriteAllText(Path.Combine(advisorAnnDir, "azure-advisor-get-annotations.md"), "annotation content");

        var advisorHaDir = Path.Combine(tempDir, "generated-advisor", "horizontal-articles");
        Directory.CreateDirectory(advisorHaDir);
        File.WriteAllText(Path.Combine(advisorHaDir, "horizontal-article-advisor.md"), """
            ---
            title: Azure Advisor overview
            description: Overview of Azure Advisor capabilities.
            ms.date: 03/27/2026
            ---

            # Azure Advisor overview

            ## Prerequisites

            You need an Azure subscription.

            ## Best practices

            Review recommendations regularly.

            ## Related content

            - [Advisor docs](/azure/advisor/)
            """);

        // generated-storage
        var storageTfDir = Path.Combine(tempDir, "generated-storage", "tool-family");
        Directory.CreateDirectory(storageTfDir);
        File.WriteAllText(Path.Combine(storageTfDir, "storage.md"), """
            ---
            title: Azure MCP Server tools for Azure Storage
            description: Storage tools.
            ms.date: 03/27/2026
            tool_count: 5
            ---

            # Azure Storage tools

            ## List accounts

            Lists storage accounts.

            ## Create account

            Creates a storage account.

            ## Related content

            - [Storage docs](/azure/storage/)
            """);

        return tempDir;
    }
}
