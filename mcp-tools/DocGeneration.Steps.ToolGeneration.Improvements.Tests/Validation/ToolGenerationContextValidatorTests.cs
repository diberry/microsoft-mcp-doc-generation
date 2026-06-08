// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared.Validation;
using ToolGeneration_Improved.Models;
using ToolGeneration_Improved.Validation;
using Xunit;

namespace DocGeneration.Steps.ToolGeneration.Improvements.Tests.Validation;

public sealed class ToolGenerationContextValidatorTests
{
    private readonly ToolGenerationContextValidator _sut = new();

    [Fact]
    public async Task ValidContext_ReturnsPass()
    {
        var context = new ToolGenerationContext("fileshares fileshare create", "# create\n\nContent.", 8000);

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task EmptyToolName_ReturnsFail()
    {
        var context = new ToolGenerationContext(string.Empty, "# create\n\nContent.", 8000);

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "ToolName");
    }

    [Fact]
    public async Task WhitespaceToolName_ReturnsFail()
    {
        var context = new ToolGenerationContext("   ", "# create\n\nContent.", 8000);

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "ToolName");
    }

    [Fact]
    public async Task EmptyComposedContent_ReturnsFail()
    {
        var context = new ToolGenerationContext("fileshares fileshare create", string.Empty, 8000);

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "ComposedContent");
    }

    [Fact]
    public async Task WrongSchemaVersion_ReturnsFail()
    {
        var context = new ToolGenerationContext("fileshares fileshare create", "Content.", 8000, "2.0");

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "SchemaVersion" && e.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public async Task MultipleInvalidFields_ReturnsAllErrors()
    {
        var context = new ToolGenerationContext(string.Empty, string.Empty, 8000, "99.0");

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 3);
    }
}
