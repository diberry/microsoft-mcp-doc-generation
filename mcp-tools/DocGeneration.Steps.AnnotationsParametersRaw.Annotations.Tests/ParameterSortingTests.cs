// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using CSharpGenerator.Generators;
using CSharpGenerator.Models;
using Xunit;

namespace CSharpGenerator.Tests;

/// <summary>
/// Tests ParameterSorting.SortByRequiredThenName.
/// Priority: P0 — parameter ordering is used in every generated file.
/// </summary>
[Collection("StaticState")]
public class ParameterSortingTests
{
    [Fact]
    public void SortByRequiredThenName_RequiredBeforeOptional()
    {
        var options = new List<Option>
        {
            TestHelpers.CreateOption("--zebra", required: false),
            TestHelpers.CreateOption("--alpha", required: true),
            TestHelpers.CreateOption("--beta", required: false),
        };

        var sorted = ParameterSorting.SortByRequiredThenName(options).ToList();

        Assert.True(sorted[0].Required);
        Assert.Equal("--alpha", sorted[0].Name);
        Assert.False(sorted[1].Required);
        Assert.False(sorted[2].Required);
    }

    [Fact]
    public void SortByRequiredThenName_PreservesRelativeOrderWithinOptionalGroup()
    {
        var options = new List<Option>
        {
            TestHelpers.CreateOption("--delta", required: false),
            TestHelpers.CreateOption("--alpha", required: false),
            TestHelpers.CreateOption("--charlie", required: false),
        };

        var sorted = ParameterSorting.SortByRequiredThenName(options).ToList();

        Assert.Equal("--delta", sorted[0].Name);
        Assert.Equal("--alpha", sorted[1].Name);
        Assert.Equal("--charlie", sorted[2].Name);
    }

    [Fact]
    public void SortByRequiredThenName_PreservesRelativeOrderWithinRequiredGroup()
    {
        var options = new List<Option>
        {
            TestHelpers.CreateOption("--zoo", required: true),
            TestHelpers.CreateOption("--alpha", required: true),
            TestHelpers.CreateOption("--middle", required: false),
        };

        var sorted = ParameterSorting.SortByRequiredThenName(options).ToList();

        Assert.Equal("--zoo", sorted[0].Name);
        Assert.Equal("--alpha", sorted[1].Name);
        Assert.Equal("--middle", sorted[2].Name);
    }

    [Fact]
    public void SortByRequiredThenName_EmptyList_ReturnsEmpty()
    {
        var sorted = ParameterSorting.SortByRequiredThenName(new List<Option>()).ToList();
        Assert.Empty(sorted);
    }

    [Fact]
    public void SortByRequiredThenName_SingleItem_ReturnsSame()
    {
        var options = new List<Option> { TestHelpers.CreateOption("--only") };
        var sorted = ParameterSorting.SortByRequiredThenName(options).ToList();

        Assert.Single(sorted);
        Assert.Equal("--only", sorted[0].Name);
    }

    [Fact]
    public void SortByRequiredThenName_DoesNotAlphabetizeWithinGroup()
    {
        var options = new List<Option>
        {
            TestHelpers.CreateOption("--Beta", required: false),
            TestHelpers.CreateOption("--alpha", required: false),
        };
        var sorted = ParameterSorting.SortByRequiredThenName(options).ToList();

        Assert.Equal("--Beta", sorted[0].Name);
        Assert.Equal("--alpha", sorted[1].Name);
    }

    [Fact]
    public void SortByRequiredThenName_NullName_HandledGracefully()
    {
        var options = new List<Option>
        {
            TestHelpers.CreateOption("--beta", required: false),
            new Option { Name = null, Required = false, Type = "string" },
        };

        var sorted = ParameterSorting.SortByRequiredThenName(options).ToList();
        Assert.Equal(2, sorted.Count);
    }

    [Fact]
    public void SortByRequiredThenName_AllRequired_PreservesRelativeOrder()
    {
        var options = new List<Option>
        {
            TestHelpers.CreateOption("--charlie", required: true),
            TestHelpers.CreateOption("--alpha", required: true),
            TestHelpers.CreateOption("--bravo", required: true),
        };

        var sorted = ParameterSorting.SortByRequiredThenName(options).ToList();

        Assert.Equal("--charlie", sorted[0].Name);
        Assert.Equal("--alpha", sorted[1].Name);
        Assert.Equal("--bravo", sorted[2].Name);
        Assert.All(sorted, o => Assert.True(o.Required));
    }

    [Fact]
    public void SortByRequiredThenName_PreservesRelativeOrderWithinBothGroups()
    {
        var options = new List<Option>
        {
            TestHelpers.CreateOption("--optional-b", required: false),
            TestHelpers.CreateOption("--required-b", required: true),
            TestHelpers.CreateOption("--optional-a", required: false),
            TestHelpers.CreateOption("--required-a", required: true),
        };

        var sorted = ParameterSorting.SortByRequiredThenName(options).ToList();

        Assert.Collection(
            sorted,
            p => Assert.Equal("--required-b", p.Name),
            p => Assert.Equal("--required-a", p.Name),
            p => Assert.Equal("--optional-b", p.Name),
            p => Assert.Equal("--optional-a", p.Name));
    }
}
