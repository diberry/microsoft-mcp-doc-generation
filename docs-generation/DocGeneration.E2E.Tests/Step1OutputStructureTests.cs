// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DocGeneration.E2E.Tests.Fixtures;
using DocGeneration.E2E.Tests.Helpers;

namespace DocGeneration.E2E.Tests;

/// <summary>
/// Validates the output directory structure after running Step 1 (annotations, parameters, raw tools)
/// for the "advisor" namespace. Ensures all expected directories exist with correct file counts.
/// </summary>
[Collection(E2ETestCollection.Name)]
[Trait("Category", "E2E")]
public sealed class Step1OutputStructureTests : E2ETestBase
{
    public Step1OutputStructureTests(PipelineOutputFixture fixture) : base(fixture) { }

    [Fact]
    public void AnnotationsDirectoryExists_WithMarkdownFiles()
    {
        if (!EnsurePipelineRan()) return;

        var dir = Path.Combine(Fixture.OutputPath, "annotations");
        Assert.True(Directory.Exists(dir), $"annotations/ directory should exist at {dir}");

        var mdFiles = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "annotations");
        Assert.True(mdFiles.Length > 0, "annotations/ should contain at least one .md file");
    }

    [Fact]
    public void ParametersDirectoryExists_WithMarkdownFiles()
    {
        if (!EnsurePipelineRan()) return;

        var dir = Path.Combine(Fixture.OutputPath, "parameters");
        Assert.True(Directory.Exists(dir), $"parameters/ directory should exist at {dir}");

        var mdFiles = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "parameters");
        Assert.True(mdFiles.Length > 0, "parameters/ should contain at least one .md file");
    }

    [Fact]
    public void RawToolsDirectoryExists_WithMarkdownFiles()
    {
        if (!EnsurePipelineRan()) return;

        var dir = Path.Combine(Fixture.OutputPath, "tools-raw");
        Assert.True(Directory.Exists(dir), $"tools-raw/ directory should exist at {dir}");

        var mdFiles = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "tools-raw");
        Assert.True(mdFiles.Length > 0, "tools-raw/ should contain at least one .md file");
    }

    [Fact]
    public void AnnotationsAndParametersHaveMatchingToolCounts()
    {
        if (!EnsurePipelineRan()) return;

        var annotationFiles = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "annotations");
        var parameterFiles = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "parameters");

        Assert.Equal(annotationFiles.Length, parameterFiles.Length);
    }

    [Fact]
    public void RawToolCountMatchesAnnotationCount()
    {
        if (!EnsurePipelineRan()) return;

        var annotationFiles = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "annotations");
        var rawToolFiles = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "tools-raw");

        Assert.Equal(annotationFiles.Length, rawToolFiles.Length);
    }

    [Fact]
    public void AllGeneratedFilesAreNonEmpty()
    {
        if (!EnsurePipelineRan()) return;

        var allMdFiles = new List<string>();
        allMdFiles.AddRange(OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "annotations"));
        allMdFiles.AddRange(OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "parameters"));
        allMdFiles.AddRange(OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "tools-raw"));

        foreach (var file in allMdFiles)
        {
            var info = new FileInfo(file);
            Assert.True(info.Length > 0, $"File should not be empty: {Path.GetFileName(file)}");
        }
    }

    [Fact]
    public void CliMetadataDirectoryExists()
    {
        if (!EnsurePipelineRan()) return;

        var cliDir = Path.Combine(Fixture.OutputPath, "cli");
        Assert.True(Directory.Exists(cliDir), "cli/ directory should exist from bootstrap");

        var cliOutputFile = Path.Combine(cliDir, "cli-output.json");
        Assert.True(File.Exists(cliOutputFile), "cli/cli-output.json should exist from bootstrap");
    }
}
