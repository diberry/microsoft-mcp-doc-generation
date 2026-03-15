// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using CSharpGenerator.Generators;
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Tests AnnotationGenerator.ConvertCamelCaseToTitleCase (internal, shared logic).
/// Integration tests for file generation are deferred due to FileNameContext dependencies.
/// Priority: P0 — annotation generation is Step 1 of the pipeline.
/// </summary>
public class AnnotationGeneratorTests
{
    // ── ConvertCamelCaseToTitleCase ─────────────────────────────

    [Theory]
    [InlineData("destructive", "Destructive")]
    [InlineData("idempotent", "Idempotent")]
    [InlineData("openWorld", "Open World")]
    [InlineData("readOnly", "Read Only")]
    [InlineData("secret", "Secret")]
    [InlineData("localRequired", "Local Required")]
    public void ConvertCamelCaseToTitleCase_MetadataKeys(string input, string expected)
    {
        var result = AnnotationGenerator.ConvertCamelCaseToTitleCase(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertCamelCaseToTitleCase_EmptyString_ReturnsEmpty()
    {
        Assert.Equal("", AnnotationGenerator.ConvertCamelCaseToTitleCase(""));
    }

    [Fact]
    public void ConvertCamelCaseToTitleCase_Null_ReturnsNull()
    {
        Assert.Null(AnnotationGenerator.ConvertCamelCaseToTitleCase(null!));
    }

    [Fact]
    public void ConvertCamelCaseToTitleCase_AllLowercase_Capitalizes()
    {
        Assert.Equal("Hello", AnnotationGenerator.ConvertCamelCaseToTitleCase("hello"));
    }

    [Fact]
    public void ConvertCamelCaseToTitleCase_SingleChar_Uppercased()
    {
        Assert.Equal("A", AnnotationGenerator.ConvertCamelCaseToTitleCase("a"));
    }

    [Fact]
    public void ConvertCamelCaseToTitleCase_AlreadyUpperFirst_Preserved()
    {
        Assert.Equal("Already Upper", AnnotationGenerator.ConvertCamelCaseToTitleCase("AlreadyUpper"));
    }
}
