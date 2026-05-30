namespace PipelineRunner.Services;

/// <summary>
/// Migration scaffold that maps AI stage step IDs to their registered reducer delegates.
/// When no reducer is registered, the step wrapper falls back to its direct upstream path.
/// Removed once all reducers introduced by Points 6–8 are merged and validated.
/// </summary>
public sealed class ReducerRegistry
{
    private readonly Dictionary<int, Func<object, CancellationToken, Task<object>>> _reducers = new();

    public void Register(int stepId, Func<object, CancellationToken, Task<object>> reducer)
    {
        ArgumentNullException.ThrowIfNull(reducer);
        _reducers[stepId] = reducer;
    }

    public bool HasReducer(int stepId) => _reducers.ContainsKey(stepId);

    public Func<object, CancellationToken, Task<object>>? GetReducer(int stepId)
        => _reducers.TryGetValue(stepId, out var reducer) ? reducer : null;
}
