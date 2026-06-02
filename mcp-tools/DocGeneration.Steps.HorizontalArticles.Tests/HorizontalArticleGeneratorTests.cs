// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Reflection;
using System.Text.Json;
using CSharpGenerator.Models;
using GenerativeAI;
using HorizontalArticleGenerator.Models;
using Xunit;
using ArticleGenerator = HorizontalArticleGenerator.Generators.HorizontalArticleGenerator;

namespace HorizontalArticleGenerator.Tests;

public class HorizontalArticleGeneratorTests : IDisposable
{
    private readonly string _outputBasePath;

    public HorizontalArticleGeneratorTests()
    {
        _outputBasePath = Path.Combine(Path.GetTempPath(), "horizontal-article-generator-tests", Guid.NewGuid().ToString("N"));
        var cliDirectory = Path.Combine(_outputBasePath, "cli");
        Directory.CreateDirectory(cliDirectory);
        File.WriteAllText(Path.Combine(cliDirectory, "cli-version.json"), "{\"version\":\"test-version\"}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputBasePath))
        {
            Directory.Delete(_outputBasePath, recursive: true);
        }
    }

    [Fact]
    public async Task ExtractStaticData_UsesToolFamilyReferenceLink()
    {
        await WriteCliOutputAsync(new CliOutput
        {
            Results =
            [
                new Tool
                {
                    Name = "list",
                    Command = "compute vm list",
                    Description = "List virtual machines.",
                    Area = "compute",
                    Option = []
                }
            ]
        });

        var generator = CreateGenerator();

        var staticData = await InvokeExtractStaticDataAsync(generator);

        Assert.Equal(1, staticData.Count);
        Assert.Equal("../tool-family/compute.md", staticData[0].ToolsReferenceLink);
    }

    // ─── Step 6 prompt-path resolution tests ──────────────────────────────────

    [Fact]
    public void Constructor_WithMcpToolsRoot_ResolvesPromptPathRelativeToMcpToolsRoot()
    {
        // Arrange: fake root that is never the CWD
        var mcpToolsRoot = Path.Combine(Path.GetTempPath(), "fake-mcp-tools-" + Guid.NewGuid().ToString("N"));
        var generator = CreateGeneratorWithMcpToolsRoot(mcpToolsRoot);

        // Act
        var resolved = generator.GetPromptPath("horizontal-article-system-prompt.txt");

        // Assert: must resolve under mcpToolsRoot, not under CWD
        var expected = Path.Combine(
            mcpToolsRoot,
            "DocGeneration.Steps.HorizontalArticles",
            "prompts",
            "horizontal-article-system-prompt.txt");
        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void Constructor_WithMcpToolsRoot_ResolvesTemplatePathRelativeToMcpToolsRoot()
    {
        var mcpToolsRoot = Path.Combine(Path.GetTempPath(), "fake-mcp-tools-" + Guid.NewGuid().ToString("N"));
        var generator = CreateGeneratorWithMcpToolsRoot(mcpToolsRoot);

        var resolved = generator.GetTemplatePath("horizontal-article-template.hbs");

        var expected = Path.Combine(
            mcpToolsRoot,
            "DocGeneration.Steps.HorizontalArticles",
            "templates",
            "horizontal-article-template.hbs");
        Assert.Equal(expected, resolved);
    }

    [Fact]
    public void Constructor_WithMcpToolsRoot_AllPromptFilesResolveUnderMcpToolsRoot()
    {
        // Regression guard: every prompt file the generator uses must resolve under mcpToolsRoot
        var mcpToolsRoot = Path.Combine(Path.GetTempPath(), "fake-mcp-tools-" + Guid.NewGuid().ToString("N"));
        var generator = CreateGeneratorWithMcpToolsRoot(mcpToolsRoot);

        string[] promptFiles =
        [
            "horizontal-article-system-prompt.txt",
            "horizontal-article-user-prompt.txt",
            "horizontal-article-tool-system-prompt.txt",
            "horizontal-article-tool-user-prompt.txt",
            "horizontal-article-namespace-user-prompt.txt",
        ];

        foreach (var file in promptFiles)
        {
            var resolved = generator.GetPromptPath(file);
            Assert.True(
                resolved.StartsWith(mcpToolsRoot, StringComparison.OrdinalIgnoreCase),
                $"Expected '{resolved}' to start with mcpToolsRoot '{mcpToolsRoot}' for prompt file '{file}'.");
        }
    }

    [Fact]
    public void Constructor_WithoutMcpToolsRoot_FallsBackToCwdForPromptPath()
    {
        // Subprocess path: generator created without mcpToolsRoot falls back to CWD-relative resolution
        var generator = CreateGenerator();

        var resolved = generator.GetPromptPath("horizontal-article-system-prompt.txt");

        var expectedCwdBased = Path.GetFullPath(
            Path.Combine(".", "DocGeneration.Steps.HorizontalArticles", "prompts", "horizontal-article-system-prompt.txt"));
        Assert.Equal(expectedCwdBased, resolved);
    }

    // ──────────────────────────────────────────────────────────────────────────

    private ArticleGenerator CreateGenerator()
    {
        return new ArticleGenerator(
            new GenerativeAIOptions
            {
                ApiKey = "test-key",
                Endpoint = "https://example.test",
                Deployment = "test-deployment",
                ApiVersion = "2024-01-01"
            },
            outputBasePath: _outputBasePath);
    }

    private static ArticleGenerator CreateGeneratorWithMcpToolsRoot(string mcpToolsRoot)
    {
        return new ArticleGenerator(
            new GenerativeAIOptions
            {
                ApiKey = "test-key",
                Endpoint = "https://example.test",
                Deployment = "test-deployment",
                ApiVersion = "2024-01-01"
            },
            mcpToolsRoot: mcpToolsRoot);
    }

    private async Task WriteCliOutputAsync(CliOutput cliOutput)
    {
        var cliOutputPath = Path.Combine(_outputBasePath, "cli", "cli-output.json");
        var json = JsonSerializer.Serialize(cliOutput);
        await File.WriteAllTextAsync(cliOutputPath, json);
    }

    private static async Task<List<StaticArticleData>> InvokeExtractStaticDataAsync(ArticleGenerator generator)
    {
        var method = typeof(ArticleGenerator).GetMethod("ExtractStaticData", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var task = method!.Invoke(generator, null) as Task<List<StaticArticleData>>;
        Assert.NotNull(task);

        return await task!;
    }
}
