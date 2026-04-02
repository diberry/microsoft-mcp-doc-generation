using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SkillsGen.Core.Fetchers;
using Xunit;

namespace SkillsGen.Core.Tests.Unit.Fetchers;

public class LocalSkillFetcherTests
{
    private readonly ILogger<LocalSkillFetcher> _logger = Substitute.For<ILogger<LocalSkillFetcher>>();

    [Fact]
    public async Task FetchAsync_ExistingSkill_ReturnsSourceFiles()
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "local-skills");
        Directory.CreateDirectory(Path.Combine(basePath, "test-skill"));
        File.WriteAllText(Path.Combine(basePath, "test-skill", "SKILL.md"), "---\nname: test\n---\n# Test");
        File.WriteAllText(Path.Combine(basePath, "test-skill", "triggers.test.ts"), "const shouldTriggerPrompts = ['hello'];");

        var fetcher = new LocalSkillFetcher(basePath, _logger);
        var result = await fetcher.FetchAsync("test-skill");

        result.Should().NotBeNull();
        result!.SkillMarkdown.Should().Contain("test");
        result.TriggersTestSource.Should().Contain("shouldTriggerPrompts");
        result.SourcePath.Should().Contain("test-skill");
    }

    [Fact]
    public async Task FetchAsync_MissingSkill_ReturnsNull()
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "local-skills-empty");
        Directory.CreateDirectory(basePath);

        var fetcher = new LocalSkillFetcher(basePath, _logger);
        var result = await fetcher.FetchAsync("nonexistent-skill");

        result.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_NoTriggerFile_ReturnsNullTriggers()
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "local-skills-notrigger");
        Directory.CreateDirectory(Path.Combine(basePath, "partial-skill"));
        File.WriteAllText(Path.Combine(basePath, "partial-skill", "SKILL.md"), "---\nname: partial\n---\n");

        var fetcher = new LocalSkillFetcher(basePath, _logger);
        var result = await fetcher.FetchAsync("partial-skill");

        result.Should().NotBeNull();
        result!.TriggersTestSource.Should().BeNull();
    }

    [Fact]
    public async Task FetchAsync_SkillNameUsedAsSubdir()
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "local-skills-path");
        Directory.CreateDirectory(Path.Combine(basePath, "azure-storage"));
        File.WriteAllText(Path.Combine(basePath, "azure-storage", "SKILL.md"), "# Storage");

        var fetcher = new LocalSkillFetcher(basePath, _logger);
        var result = await fetcher.FetchAsync("azure-storage");

        result.Should().NotBeNull();
        result!.SourcePath.Should().Contain("azure-storage");
    }

    [Fact]
    public async Task FetchAsync_WithTestsPath_FindsTriggersInTestsDir()
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "local-skills-testspath");
        var testsPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "local-skills-testspath-tests");
        Directory.CreateDirectory(Path.Combine(basePath, "azure-test"));
        Directory.CreateDirectory(Path.Combine(testsPath, "azure-test"));
        File.WriteAllText(Path.Combine(basePath, "azure-test", "SKILL.md"), "---\nname: azure-test\n---\n# Test");
        File.WriteAllText(Path.Combine(testsPath, "azure-test", "triggers.test.ts"),
            "const shouldTriggerPrompts = ['from tests dir'];");

        var fetcher = new LocalSkillFetcher(basePath, _logger, testsPath);
        var result = await fetcher.FetchAsync("azure-test");

        result.Should().NotBeNull();
        result!.TriggersTestSource.Should().Contain("from tests dir");
    }

    [Fact]
    public async Task FetchAsync_WithTestsPath_FallsBackToColocated()
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "local-skills-fallback");
        var testsPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "local-skills-fallback-tests");
        Directory.CreateDirectory(Path.Combine(basePath, "azure-test"));
        Directory.CreateDirectory(testsPath); // empty tests dir — no trigger here
        File.WriteAllText(Path.Combine(basePath, "azure-test", "SKILL.md"), "---\nname: azure-test\n---\n# Test");
        File.WriteAllText(Path.Combine(basePath, "azure-test", "triggers.test.ts"),
            "const shouldTriggerPrompts = ['from colocated'];");

        var fetcher = new LocalSkillFetcher(basePath, _logger, testsPath);
        var result = await fetcher.FetchAsync("azure-test");

        result.Should().NotBeNull();
        result!.TriggersTestSource.Should().Contain("from colocated");
    }

    [Fact]
    public async Task FetchAsync_NullTestsPath_UsesColocatedPath()
    {
        var basePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "local-skills-nulltests");
        Directory.CreateDirectory(Path.Combine(basePath, "azure-test"));
        File.WriteAllText(Path.Combine(basePath, "azure-test", "SKILL.md"), "---\nname: azure-test\n---\n# Test");
        File.WriteAllText(Path.Combine(basePath, "azure-test", "triggers.test.ts"),
            "const shouldTriggerPrompts = ['colocated triggers'];");

        var fetcher = new LocalSkillFetcher(basePath, _logger, testsPath: null);
        var result = await fetcher.FetchAsync("azure-test");

        result.Should().NotBeNull();
        result!.TriggersTestSource.Should().Contain("colocated triggers");
    }
}
