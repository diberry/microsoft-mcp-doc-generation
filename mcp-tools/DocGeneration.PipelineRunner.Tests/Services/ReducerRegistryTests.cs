using PipelineRunner.Services;
using Xunit;

namespace PipelineRunner.Tests.Services;

public sealed class ReducerRegistryTests
{
    [Fact]
    public void Register_StoresReducerForStep()
    {
        var registry = new ReducerRegistry();
        Func<object, CancellationToken, Task<object>> reducer = (input, cancellationToken) => Task.FromResult((object)"typed-context");

        registry.Register(3, reducer);

        var registered = registry.GetReducer(3);

        Assert.True(registry.HasReducer(3));
        Assert.Same(reducer, registered);
    }

    [Fact]
    public void HasReducer_ReturnsFalse_WhenReducerNotRegistered()
    {
        var registry = new ReducerRegistry();

        var hasReducer = registry.HasReducer(6);

        Assert.False(hasReducer);
    }

    [Fact]
    public void GetReducer_ReturnsNull_WhenReducerNotRegistered()
    {
        var registry = new ReducerRegistry();

        var reducer = registry.GetReducer(4);

        Assert.Null(reducer);
    }

    [Fact]
    public void Register_OverwritesExistingReducer_ForSameStepId()
    {
        var registry = new ReducerRegistry();
        Func<object, CancellationToken, Task<object>> firstReducer = (input, cancellationToken) => Task.FromResult((object)"first");
        Func<object, CancellationToken, Task<object>> secondReducer = (input, cancellationToken) => Task.FromResult((object)"second");

        registry.Register(4, firstReducer);
        registry.Register(4, secondReducer);

        var reducer = registry.GetReducer(4);

        Assert.Same(secondReducer, reducer);
    }
}
