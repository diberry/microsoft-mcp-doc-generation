using Shared;
using Xunit;

namespace DocGeneration.Core.Shared.Tests;

public class ParameterSortingTests
{
    [Fact]
    public void SortByRequiredThenName_Generic_ReturnsRequiredItemsFirst_Bug743()
    {
        var parameters = new[]
        {
            new ParameterFixture("storage optional", false),
            new ParameterFixture("key vault required", true),
            new ParameterFixture("cosmos optional", false)
        };

        var sorted = ParameterSorting.SortByRequiredThenName(parameters, p => p.IsRequired).ToList();

        Assert.Equal(
            [
                "key vault required",
                "storage optional",
                "cosmos optional"
            ],
            sorted.Select(p => p.Name));
    }

    [Fact]
    public void SortByRequiredThenName_Generic_PreservesSourceOrderWithinRequiredAndOptionalGroups_Bug743()
    {
        var parameters = new[]
        {
            new ParameterFixture("storage optional first", false),
            new ParameterFixture("key vault required first", true),
            new ParameterFixture("cosmos required second", true),
            new ParameterFixture("monitor optional second", false)
        };

        var sorted = ParameterSorting.SortByRequiredThenName(parameters, p => p.IsRequired).ToList();

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
    public void SortByRequiredThenName_Generic_ReturnsEmptySequenceForEmptyInput_Bug743()
    {
        var sorted = ParameterSorting
            .SortByRequiredThenName(Array.Empty<ParameterFixture>(), p => p.IsRequired)
            .ToList();

        Assert.Empty(sorted);
    }

    private sealed record ParameterFixture(string Name, bool IsRequired);
}
