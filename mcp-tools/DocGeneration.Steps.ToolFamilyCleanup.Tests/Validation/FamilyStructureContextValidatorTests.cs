// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared.Validation;
using ToolFamilyCleanup.Models;
using ToolFamilyCleanup.Validation;
using Xunit;

namespace DocGeneration.Steps.ToolFamilyCleanup.Tests.Validation;

public sealed class FamilyStructureContextValidatorTests
{
    private readonly FamilyStructureContextValidator _sut = new();

    private static FamilyStructureContext ValidContext() => new(
        "fileshares",
        [new FamilySection("Get fileshare", ["fileshares fileshare get"], "## Get fileshare\n\nContent.")],
        "1.0");

    [Fact]
    public async Task ValidContext_ReturnsPass()
    {
        var result = await _sut.ValidateAsync(ValidContext(), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task EmptyFamilyName_ReturnsFail()
    {
        var context = ValidContext() with { FamilyName = string.Empty };

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "FamilyName");
    }

    [Fact]
    public async Task EmptySections_ReturnsFail()
    {
        var context = new FamilyStructureContext("fileshares", [], "1.0");

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "Sections");
    }

    [Fact]
    public async Task SectionWithEmptyHeading_ReturnsFail()
    {
        var context = new FamilyStructureContext(
            "fileshares",
            [new FamilySection(string.Empty, ["fileshares fileshare get"], "Content.")],
            "1.0");

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field.StartsWith("Sections[") && e.Field.EndsWith(".Heading"));
    }

    [Fact]
    public async Task WrongSchemaVersion_ReturnsFail()
    {
        var context = ValidContext() with { SchemaVersion = "2.0" };

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Field == "SchemaVersion" && e.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public async Task MultipleSectionsAllValid_ReturnsPass()
    {
        var context = new FamilyStructureContext(
            "fileshares",
            [
                new FamilySection("Get fileshare", ["fileshares fileshare get"], "Content A."),
                new FamilySection("List fileshares", ["fileshares fileshare list"], "Content B.")
            ],
            "1.0");

        var result = await _sut.ValidateAsync(context, CancellationToken.None);

        Assert.True(result.IsValid);
    }
}
