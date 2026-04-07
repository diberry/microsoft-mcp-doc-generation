// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DocGeneration.E2E.Tests.Fixtures;
using DocGeneration.E2E.Tests.Helpers;

namespace DocGeneration.E2E.Tests;

/// <summary>
/// Validates cross-step consistency: annotations, parameters, and raw tools
/// should all reference the same set of tools for the "advisor" namespace.
/// </summary>
[Collection(E2ETestCollection.Name)]
[Trait("Category", "E2E")]
public sealed class CrossStepConsistencyTests : E2ETestBase
{
    public CrossStepConsistencyTests(PipelineOutputFixture fixture) : base(fixture) { }

    [Fact]
    public void EveryAnnotationFileHasMatchingParameterFile()
    {
        if (!EnsurePipelineRan()) return;

        var annotationBaseNames = OutputValidator.GetToolBaseNames(
            Fixture.OutputPath, "annotations", "-annotations");
        var parameterBaseNames = OutputValidator.GetToolBaseNames(
            Fixture.OutputPath, "parameters", "-parameters");

        var missingInParameters = annotationBaseNames.Except(parameterBaseNames).ToList();
        Assert.True(
            missingInParameters.Count == 0,
            $"Annotations exist without matching parameters: {string.Join(", ", missingInParameters)}");
    }

    [Fact]
    public void EveryParameterFileHasMatchingAnnotationFile()
    {
        if (!EnsurePipelineRan()) return;

        var annotationBaseNames = OutputValidator.GetToolBaseNames(
            Fixture.OutputPath, "annotations", "-annotations");
        var parameterBaseNames = OutputValidator.GetToolBaseNames(
            Fixture.OutputPath, "parameters", "-parameters");

        var missingInAnnotations = parameterBaseNames.Except(annotationBaseNames).ToList();
        Assert.True(
            missingInAnnotations.Count == 0,
            $"Parameters exist without matching annotations: {string.Join(", ", missingInAnnotations)}");
    }

    [Fact]
    public void RawToolBaseNamesMatchAnnotationBaseNames()
    {
        if (!EnsurePipelineRan()) return;

        var annotationBaseNames = OutputValidator.GetToolBaseNames(
            Fixture.OutputPath, "annotations", "-annotations");

        var rawToolFiles = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "tools-raw");
        var rawToolBaseNames = rawToolFiles
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingRawTools = annotationBaseNames.Except(rawToolBaseNames).ToList();
        var extraRawTools = rawToolBaseNames.Except(annotationBaseNames).ToList();

        Assert.True(
            missingRawTools.Count == 0,
            $"Annotations without matching raw tools: {string.Join(", ", missingRawTools)}");
        Assert.True(
            extraRawTools.Count == 0,
            $"Raw tools without matching annotations: {string.Join(", ", extraRawTools)}");
    }

    [Fact]
    public void RawToolFilesContainCommandComment()
    {
        if (!EnsurePipelineRan()) return;

        var rawToolFiles = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "tools-raw");
        Assert.NotEmpty(rawToolFiles);

        foreach (var file in rawToolFiles)
        {
            var content = File.ReadAllText(file);
            Assert.True(
                content.Contains("azmcp", StringComparison.OrdinalIgnoreCase),
                $"Raw tool file should reference azmcp command: {Path.GetFileName(file)}");
        }
    }

    [Fact]
    public void ParameterFilesHaveSubstantialContent()
    {
        if (!EnsurePipelineRan()) return;

        var parameterFiles = OutputValidator.GetMarkdownFiles(Fixture.OutputPath, "parameters");
        Assert.NotEmpty(parameterFiles);

        foreach (var file in parameterFiles)
        {
            var content = File.ReadAllText(file);
            // Parameter files should have more than just frontmatter (at minimum a table or content)
            Assert.True(
                content.Length > 100,
                $"Parameter file suspiciously short ({content.Length} chars): {Path.GetFileName(file)}");
        }
    }
}
