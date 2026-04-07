// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DocGeneration.E2E.Tests.Fixtures;
using DocGeneration.E2E.Tests.Helpers;

namespace DocGeneration.E2E.Tests;

/// <summary>
/// Validates YAML frontmatter in all generated .md files from Step 1.
/// Checks that annotations use ms.topic: include, raw tools use ms.topic: reference,
/// and all files contain required metadata fields.
/// </summary>
[Collection(E2ETestCollection.Name)]
[Trait("Category", "E2E")]
public sealed class FrontmatterValidationTests : E2ETestBase
{
    public FrontmatterValidationTests(PipelineOutputFixture fixture) : base(fixture) { }

    [Fact]
    public void AnnotationFiles_HaveValidFrontmatter()
    {
        if (!EnsurePipelineRan()) return;

        var files = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "annotations");
        Assert.NotEmpty(files);

        foreach (var file in files)
        {
            var content = File.ReadAllText(file);
            var frontmatter = FrontmatterParser.Parse(content);
            Assert.True(frontmatter is not null, $"Missing frontmatter in {Path.GetFileName(file)}");
        }
    }

    [Fact]
    public void AnnotationFiles_HaveMsTopicInclude()
    {
        if (!EnsurePipelineRan()) return;

        var files = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "annotations");
        foreach (var file in files)
        {
            var frontmatter = FrontmatterParser.ParseFile(file);
            Assert.NotNull(frontmatter);
            Assert.True(
                frontmatter.TryGetValue("ms.topic", out var topic),
                $"Missing ms.topic in {Path.GetFileName(file)}");
            Assert.Equal("include", topic);
        }
    }

    [Fact]
    public void ParameterFiles_HaveMsTopicInclude()
    {
        if (!EnsurePipelineRan()) return;

        var files = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "parameters");
        foreach (var file in files)
        {
            var frontmatter = FrontmatterParser.ParseFile(file);
            Assert.NotNull(frontmatter);
            Assert.True(
                frontmatter.TryGetValue("ms.topic", out var topic),
                $"Missing ms.topic in {Path.GetFileName(file)}");
            Assert.Equal("include", topic);
        }
    }

    [Fact]
    public void RawToolFiles_HaveMsTopicReference()
    {
        if (!EnsurePipelineRan()) return;

        var files = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "tools-raw");
        foreach (var file in files)
        {
            var frontmatter = FrontmatterParser.ParseFile(file);
            Assert.NotNull(frontmatter);
            Assert.True(
                frontmatter.TryGetValue("ms.topic", out var topic),
                $"Missing ms.topic in {Path.GetFileName(file)}");
            Assert.Equal("reference", topic);
        }
    }

    [Fact]
    public void AllFiles_HaveMcpCliVersion()
    {
        if (!EnsurePipelineRan()) return;

        var allFiles = GetAllStep1MarkdownFiles();
        foreach (var file in allFiles)
        {
            var frontmatter = FrontmatterParser.ParseFile(file);
            Assert.NotNull(frontmatter);
            Assert.True(
                frontmatter.ContainsKey("mcp-cli.version"),
                $"Missing mcp-cli.version in {Path.GetFileName(file)}");
        }
    }

    [Fact]
    public void AllFiles_HaveMsDate()
    {
        if (!EnsurePipelineRan()) return;

        var allFiles = GetAllStep1MarkdownFiles();
        foreach (var file in allFiles)
        {
            var frontmatter = FrontmatterParser.ParseFile(file);
            Assert.NotNull(frontmatter);
            Assert.True(
                frontmatter.ContainsKey("ms.date"),
                $"Missing ms.date in {Path.GetFileName(file)}");

            var msDate = frontmatter["ms.date"];
            Assert.False(
                string.IsNullOrWhiteSpace(msDate),
                $"ms.date is empty in {Path.GetFileName(file)}");
        }
    }

    [Fact]
    public void AnnotationAndParameterFiles_HaveGeneratedField()
    {
        if (!EnsurePipelineRan()) return;

        var annotationFiles = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "annotations");
        var parameterFiles = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "parameters");

        foreach (var file in annotationFiles.Concat(parameterFiles))
        {
            var frontmatter = FrontmatterParser.ParseFile(file);
            Assert.NotNull(frontmatter);
            Assert.True(
                frontmatter.ContainsKey("generated"),
                $"Missing 'generated' field in {Path.GetFileName(file)}");
        }
    }

    private IEnumerable<string> GetAllStep1MarkdownFiles()
    {
        return OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "annotations")
            .Concat(OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "parameters"))
            .Concat(OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "tools-raw"));
    }
}
