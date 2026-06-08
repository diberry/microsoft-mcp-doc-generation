// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using HorizontalArticleGenerator.Models;
using HorizontalArticleGenerator.Validation;
using Xunit;
using Shared.Validation;

namespace DocGeneration.Steps.HorizontalArticles.Tests.Validation;

public sealed class ArticleOutlineContextValidatorTests
{
    private readonly ArticleOutlineContextValidator _sut = new();

    private static ArticleOutlineContext ValidContext() => new(
        "Azure MCP Server tools overview",
        [
            new ArticleOutlineSection("Prerequisites", "prerequisites", ["Have an Azure subscription.", "Install the CLI."]),
            new ArticleOutlineSection("Getting started", "howto", ["Run az mcp server start.", "Connect your client."])
        ],
        "azure-mcp",
        "1.0");

    [Fact]
    public async Task ValidContext_ReturnsPass()
    {
        var result = await _sut.ValidateAsync(ValidContext(), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task EmptyArticleTitle_ReturnsFail()
    {
        var context = ValidContext() with { ArticleTitle = string.Empty };

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "ArticleTitle");
    }

    [Fact]
    public async Task OnlyOneSection_ReturnsFail()
    {
        var context = new ArticleOutlineContext(
            "Title",
            [new ArticleOutlineSection("Prerequisites", "prerequisites", ["Have an Azure subscription."])],
            "azure-mcp");

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Sections");
    }

    [Fact]
    public async Task EmptySections_ReturnsFail()
    {
        var context = new ArticleOutlineContext("Title", [], "azure-mcp");

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Sections");
    }

    [Fact]
    public async Task SectionWithNoEvidenceItems_ReturnsFail()
    {
        var context = new ArticleOutlineContext(
            "Title",
            [
                new ArticleOutlineSection("Prerequisites", "prerequisites", []),
                new ArticleOutlineSection("Getting started", "howto", ["Run az mcp server start."])
            ],
            "azure-mcp");

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field.StartsWith("Sections[") && e.Field.EndsWith(".EvidenceItems"));
    }

    [Fact]
    public async Task WrongSchemaVersion_ReturnsFail()
    {
        var context = ValidContext() with { SchemaVersion = "99.0" };

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "SchemaVersion" && e.Severity == ValidationSeverity.Error);
    }
}
