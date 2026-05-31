using Shared.Validation;

namespace PipelineRunner.Services;

/// <summary>
/// Central registry for AI stage reducers and pre-AI validators.
/// Reducers are keyed by step ID; validators are keyed by context type.
/// </summary>
public sealed class ReducerRegistry
{
    private readonly Dictionary<int, Func<object, CancellationToken, Task<object>>> _reducers = new();
    private readonly Dictionary<Type, List<IPreAiValidator>> _validators = new();

    public void Register(int stepId, Func<object, CancellationToken, Task<object>> reducer)
    {
        ArgumentNullException.ThrowIfNull(reducer);
        _reducers[stepId] = reducer;
    }

    public void RegisterValidator<TContext>(IPreAiValidator<TContext> validator)
    {
        ArgumentNullException.ThrowIfNull(validator);
        var key = typeof(TContext);
        if (!_validators.TryGetValue(key, out var list))
        {
            list = new List<IPreAiValidator>();
            _validators[key] = list;
        }
        list.Add(validator);
    }

    public IEnumerable<IPreAiValidator<TContext>> GetValidators<TContext>()
    {
        if (!_validators.TryGetValue(typeof(TContext), out var list))
        {
            return Enumerable.Empty<IPreAiValidator<TContext>>();
        }
        return list.OfType<IPreAiValidator<TContext>>();
    }

    internal IEnumerable<IPreAiValidator> GetValidatorsForType(Type contextType)
    {
        if (!_validators.TryGetValue(contextType, out var list))
        {
            return Enumerable.Empty<IPreAiValidator>();
        }
        return list;
    }

    public bool HasReducer(int stepId) => _reducers.ContainsKey(stepId);

    public Func<object, CancellationToken, Task<object>>? GetReducer(int stepId)
        => _reducers.TryGetValue(stepId, out var reducer) ? reducer : null;

    /// <summary>
    /// Aggregates results from all validators for a given context.
    /// Returns <see cref="PreAiValidationResult.Pass()"/> when no validators are registered.
    /// </summary>
    public static async Task<PreAiValidationResult> AggregateAsync<TContext>(
        IEnumerable<IPreAiValidator<TContext>> validators,
        TContext context,
        CancellationToken cancellationToken)
    {
        var validatorList = validators as IReadOnlyList<IPreAiValidator<TContext>> ?? validators.ToList();
        if (validatorList.Count == 0)
        {
            return PreAiValidationResult.Pass();
        }

        var allErrors = new List<ValidationError>();
        int? estimatedTokens = null;
        int? tokenBudget = null;

        foreach (var validator in validatorList)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            allErrors.AddRange(result.Errors);
            if (result.EstimatedPromptTokens.HasValue)
            {
                estimatedTokens = result.EstimatedPromptTokens;
            }
            if (result.TokenBudget.HasValue)
            {
                tokenBudget = result.TokenBudget;
            }
        }

        var isValid = allErrors.All(static e => e.Severity != ValidationSeverity.Error);
        return new PreAiValidationResult(isValid, allErrors, estimatedTokens, tokenBudget);
    }
}
