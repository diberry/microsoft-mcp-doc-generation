using PipelineRunner.Services;
using Shared.Validation;
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

    [Fact]
    public void RegisterValidator_StoresValidatorByContextType()
    {
        var registry = new ReducerRegistry();
        var validator = new AlwaysPassValidator();

        registry.RegisterValidator(validator);

        var validators = registry.GetValidators<string>();

        Assert.Contains(validator, validators);
    }

    [Fact]
    public void GetValidators_ReturnsEmpty_WhenNoneRegistered()
    {
        var registry = new ReducerRegistry();

        var validators = registry.GetValidators<int>();

        Assert.Empty(validators);
    }

    [Fact]
    public async Task AggregateAsync_ReturnsPass_WhenNoValidators()
    {
        var result = await ReducerRegistry.AggregateAsync<string>([], "context", CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task AggregateAsync_AggregatesErrors_FromMultipleValidators()
    {
        var validators = new IPreAiValidator<string>[]
        {
            new AlwaysFailValidator("error-one"),
            new AlwaysFailValidator("error-two"),
        };

        var result = await ReducerRegistry.AggregateAsync<string>(validators, "context", CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, e => e.Message == "error-one");
        Assert.Contains(result.Errors, e => e.Message == "error-two");
    }

    private sealed class AlwaysPassValidator : IPreAiValidator<string>
    {
        public Type ContextType => typeof(string);

        public Task<PreAiValidationResult> ValidateAsync(string context, CancellationToken cancellationToken)
            => Task.FromResult(PreAiValidationResult.Pass());

        public Task<PreAiValidationResult> ValidateAsync(object context, CancellationToken cancellationToken)
            => ValidateAsync((string)context, cancellationToken);
    }

    private sealed class AlwaysFailValidator(string message) : IPreAiValidator<string>
    {
        public Type ContextType => typeof(string);

        public Task<PreAiValidationResult> ValidateAsync(string context, CancellationToken cancellationToken)
            => Task.FromResult(PreAiValidationResult.Fail(new ValidationError("field", message, ValidationSeverity.Error)));

        public Task<PreAiValidationResult> ValidateAsync(object context, CancellationToken cancellationToken)
            => ValidateAsync((string)context, cancellationToken);
    }
}
