using Shared;
using Xunit;

namespace DocGeneration.Core.Shared.Tests;

public class ParameterSortingTests
{
    [Fact]
    public void SortRequiredFirstStable_Generic_ReturnsRequiredItemsFirst_Bug743()
    {
        var parameters = new[]
        {
            new ParameterFixture("storage optional", false),
            new ParameterFixture("key vault required", true),
            new ParameterFixture("cosmos optional", false)
        };

        var sorted = ParameterSorting.SortRequiredFirstStable(parameters, p => p.IsRequired).ToList();

        Assert.Equal(
            [
                "key vault required",
                "storage optional",
                "cosmos optional"
            ],
            sorted.Select(p => p.Name));
    }

    [Fact]
    public void SortRequiredFirstStable_Generic_PreservesSourceOrderWithinRequiredAndOptionalGroups_Bug743()
    {
        var parameters = new[]
        {
            new ParameterFixture("storage optional first", false),
            new ParameterFixture("key vault required first", true),
            new ParameterFixture("cosmos required second", true),
            new ParameterFixture("monitor optional second", false)
        };

        var sorted = ParameterSorting.SortRequiredFirstStable(parameters, p => p.IsRequired).ToList();

        Assert.Equal(
            [
                "key vault required first",
                "cosmos required second",
                "storage optional first",
                "monitor optional second"
            ],
            sorted.Select(p => p.Name));
    }

    [Fact]
    public void SortRequiredFirstStable_Generic_ReturnsEmptySequenceForEmptyInput_Bug743()
    {
        var sorted = ParameterSorting
            .SortRequiredFirstStable(Array.Empty<ParameterFixture>(), p => p.IsRequired)
            .ToList();

        Assert.Empty(sorted);
    }

    [Fact]
    public void SortRequiredFirstStable_Generic_PreservesSourceOrderWhenAllRequired_Bug743()
    {
        var parameters = new[]
        {
            new ParameterFixture("storage account required", true),
            new ParameterFixture("key vault secret required", true),
            new ParameterFixture("cosmos database required", true),
            new ParameterFixture("monitor alert required", true)
        };

        var sorted = ParameterSorting.SortRequiredFirstStable(parameters, p => p.IsRequired).ToList();

        Assert.Equal(
            [
                "storage account required",
                "key vault secret required",
                "cosmos database required",
                "monitor alert required"
            ],
            sorted.Select(p => p.Name));
    }

    [Fact]
    public void SortRequiredFirstStable_Generic_PreservesSourceOrderWhenAllOptional_Bug743()
    {
        var parameters = new[]
        {
            new ParameterFixture("monitor optional first", false),
            new ParameterFixture("cosmos optional second", false),
            new ParameterFixture("key vault optional third", false),
            new ParameterFixture("storage optional fourth", false)
        };

        var sorted = ParameterSorting.SortRequiredFirstStable(parameters, p => p.IsRequired).ToList();

        Assert.Equal(
            [
                "monitor optional first",
                "cosmos optional second",
                "key vault optional third",
                "storage optional fourth"
            ],
            sorted.Select(p => p.Name));
    }

    private sealed record ParameterFixture(string Name, bool IsRequired);
}
