namespace PipelineRunner.Services;

/// <summary>
/// Migration scaffold: maps AI stage step IDs to their optional reducer delegates.
/// When no reducer is registered, step wrappers fall back to their pre-Point-6 direct paths.
/// This registry will be removed after all Point 6 reducers are merged and validated.
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
