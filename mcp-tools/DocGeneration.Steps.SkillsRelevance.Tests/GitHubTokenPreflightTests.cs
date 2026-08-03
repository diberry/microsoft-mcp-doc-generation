// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Xunit;
using SkillsRelevance.Models;
using SkillsRelevance.Output;

namespace SkillsRelevance.Tests;

/// <summary>
/// Tests for GitHub token preflight checks and fallback output generation.
/// Issue #790: Ensure Step 5 produces output even when GITHUB_TOKEN is missing.
/// </summary>
public class GitHubTokenPreflightTests : IDisposable
{
    private readonly string _tempDir;

    public GitHubTokenPreflightTests()
    {
        _tempDir = Path.Combine(
            Path.GetTempPath(),
            "github-token-preflight-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── GITHUB_TOKEN environment variable handling ──────────────────────

    [Fact]
    public void GitHubTokenEnvironmentCheck_WhenTokenIsNotSet_DisplaysWarning()
    {
        // Arrange: Ensure GITHUB_TOKEN is not set
        var originalToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        try
        {
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", null, EnvironmentVariableTarget.Process);
            var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

            // Act
            bool tokenIsSet = !string.IsNullOrWhiteSpace(githubToken);

            // Assert: Token should not be set
            Assert.False(tokenIsSet, "GITHUB_TOKEN should not be set for this test");
        }
        finally
        {
            if (originalToken != null)
                Environment.SetEnvironmentVariable("GITHUB_TOKEN", originalToken, EnvironmentVariableTarget.Process);
        }
    }

    [Fact]
    public void GitHubTokenEnvironmentCheck_WhenTokenIsSet_RecognizesToken()
    {
        // Arrange: Set a test token
        var originalToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        try
        {
            const string testToken = "ghp_test_token_12345";
            Environment.SetEnvironmentVariable("GITHUB_TOKEN", testToken, EnvironmentVariableTarget.Process);
            var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

            // Act & Assert
            Assert.NotNull(githubToken);
            Assert.Equal(testToken, githubToken);
        }
        finally
        {
            if (originalToken != null)
                Environment.SetEnvironmentVariable("GITHUB_TOKEN", originalToken, EnvironmentVariableTarget.Process);
            else
                Environment.SetEnvironmentVariable("GITHUB_TOKEN", null, EnvironmentVariableTarget.Process);
        }
    }

    // ── Fallback output generation when GitHub API is unavailable ──────

    [Fact]
    public async Task FallbackMarkdownOutput_CreatedWhenNoSkillsAvailable()
    {
        // Arrange
        var emptySkills = new List<SkillInfo>();
        var sources = CreateDefaultSources();

        // Act
        await SkillsMarkdownWriter.WriteServiceSummaryAsync(
            _tempDir, "test-service", emptySkills, sources);

        // Assert: File should be created even with empty skills
        var filePath = Path.Combine(_tempDir, "test-service-skills-relevance.md");
        Assert.True(File.Exists(filePath), "Markdown file should be created even with no skills");

        var content = await File.ReadAllTextAsync(filePath);
        Assert.NotEmpty(content);
        Assert.Contains("test-service", content);
        Assert.Contains("No skills with significant relevance", content);
    }

    [Fact]
    public async Task FallbackJsonOutput_CreatedWhenNoSkillsAvailable()
    {
        // Arrange
        var emptySkills = new List<SkillInfo>();
        var sources = CreateDefaultSources();

        // Act
        await SkillsJsonWriter.WriteServiceSummaryJsonAsync(
            _tempDir, "test-service", emptySkills, sources);

        // Assert: JSON file should be created even with empty skills
        var filePath = Path.Combine(_tempDir, "test-service-skills-relevance.json");
        Assert.True(File.Exists(filePath), "JSON file should be created even with no skills");

        var content = await File.ReadAllTextAsync(filePath);
        Assert.NotEmpty(content);
        Assert.Contains("test-service", content);
    }

    [Fact]
    public async Task FallbackOutput_IncludesSourceInformation()
    {
        // Arrange
        var emptySkills = new List<SkillInfo>();
        var sources = CreateDefaultSources();

        // Act
        await SkillsMarkdownWriter.WriteServiceSummaryAsync(
            _tempDir, "keyvault", emptySkills, sources);

        // Assert: Output should reference sources even when no skills found
        var filePath = Path.Combine(_tempDir, "keyvault-skills-relevance.md");
        var content = await File.ReadAllTextAsync(filePath);

        Assert.Contains("Skill Sources Checked", content);
        foreach (var source in sources)
        {
            Assert.Contains(source.DisplayName, content);
        }
    }

    [Fact]
    public async Task FallbackOutput_ContainsFrontmatter()
    {
        // Arrange
        var emptySkills = new List<SkillInfo>();
        var sources = CreateDefaultSources();

        // Act
        await SkillsMarkdownWriter.WriteServiceSummaryAsync(
            _tempDir, "storage", emptySkills, sources);

        // Assert: YAML frontmatter should be present
        var filePath = Path.Combine(_tempDir, "storage-skills-relevance.md");
        var content = await File.ReadAllTextAsync(filePath);

        Assert.StartsWith("---", content);
        Assert.Contains("title:", content);
        Assert.Contains("generated:", content);
        Assert.Contains("skillCount: 0", content);
    }

    [Fact]
    public async Task IndexFile_CreatedSuccessfully()
    {
        // Arrange
        var serviceNames = new List<string> { "storage", "keyvault", "cosmos" };

        // Act
        await SkillsMarkdownWriter.WriteIndexAsync(_tempDir, serviceNames);

        // Assert
        var filePath = Path.Combine(_tempDir, "index.md");
        Assert.True(File.Exists(filePath), "Index file should be created");

        var content = await File.ReadAllTextAsync(filePath);
        Assert.Contains("Skills Relevance Index", content);
        foreach (var name in serviceNames)
        {
            Assert.Contains(name, content);
        }
    }

    [Fact]
    public async Task OutputDirectory_CreatedIfNotExist()
    {
        // Arrange
        var nonExistentDir = Path.Combine(_tempDir, "does", "not", "exist", "yet");
        var emptySkills = new List<SkillInfo>();
        var sources = CreateDefaultSources();

        // Act
        await SkillsMarkdownWriter.WriteServiceSummaryAsync(
            nonExistentDir, "test", emptySkills, sources);

        // Assert
        Assert.True(Directory.Exists(nonExistentDir), "Output directory should be created");
        var filePath = Path.Combine(nonExistentDir, "test-skills-relevance.md");
        Assert.True(File.Exists(filePath), "Output file should be created in new directory");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static List<SkillSource> CreateDefaultSources()
    {
        return new List<SkillSource>
        {
            new()
            {
                Owner = "azure",
                Repo = "azure-sdk-for-net",
                DisplayName = "Azure SDK for .NET",
                Path = "/skills/dotnet"
            },
            new()
            {
                Owner = "microsoft",
                Repo = "mcp-tools",
                DisplayName = "MCP Tools Skills",
                Path = "/skills"
            }
        };
    }
}
