// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Shared.Validation;
using Xunit;

namespace Shared.Tests.Validation;

public class PreAiValidatorRegistryTests
{
    [Fact]
    public async Task ValidateAsync_ValidContext_Passes()
    {
        var registry = new PreAiValidatorRegistry();
        registry.DeclareStage<TestContext>("tool-generation");
        registry.Register("tool-generation", new PassingValidator());

        var result = await registry.ValidateAsync("tool-generation", new TestContext("storage"), CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.True(result.WithinBudget);
        Assert.Equal(120, result.EstimatedPromptTokens);
        Assert.Equal(400, result.TokenBudget);
    }

    [Fact]
    public void Register_ContextMismatch_Throws()
    {
        var registry = new PreAiValidatorRegistry();
        registry.DeclareStage<TestContext>("example-prompts");

        var exception = Assert.Throws<ValidatorContextMismatchException>(
            () => registry.Register("example-prompts", new DifferentContextValidator()));

        Assert.Equal("example-prompts", exception.StageName);
        Assert.Equal(typeof(TestContext), exception.DeclaredType);
        Assert.Equal(typeof(DifferentContext), exception.AttemptedType);
    }

    [Fact]
    public async Task ValidateAsync_MultipleValidators_AggregatesErrors()
    {
        var registry = new PreAiValidatorRegistry();
        registry.DeclareStage<TestContext>("horizontal-articles");
        registry.Register("horizontal-articles", new WarningValidator());
        registry.Register("horizontal-articles", new ErrorValidator());

        var result = await registry.ValidateAsync("horizontal-articles", new TestContext("monitor"), CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains(result.Errors, error => error.Field == "name" && error.Severity == ValidationSeverity.Warning);
        Assert.Contains(result.Errors, error => error.Field == "description" && error.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void Pass_CreatesValidResult()
    {
        var result = PreAiValidationResult.Pass(estimatedTokens: 55, budget: 100);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Equal(55, result.EstimatedPromptTokens);
        Assert.Equal(100, result.TokenBudget);
        Assert.True(result.WithinBudget);
    }

    [Fact]
    public void Fail_CreatesInvalidResult()
    {
        var result = PreAiValidationResult.Fail(
            new ValidationError("title", "Title is required.", ValidationSeverity.Error),
            new ValidationError("summary", "Summary is long.", ValidationSeverity.Warning));

        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Null(result.EstimatedPromptTokens);
        Assert.Null(result.TokenBudget);
    }

    [Fact]
    public void HasValidators_UnregisteredStage_ReturnsFalse()
    {
        var registry = new PreAiValidatorRegistry();

        Assert.False(registry.HasValidators("missing-stage"));
    }

    private sealed record TestContext(string Name);

    private sealed record DifferentContext(string Name);

    private sealed class PassingValidator : IPreAiValidator<TestContext>
    {
        public Task<PreAiValidationResult> ValidateAsync(TestContext context, CancellationToken cancellationToken)
            => Task.FromResult(PreAiValidationResult.Pass(estimatedTokens: 120, budget: 400));
    }

    private sealed class DifferentContextValidator : IPreAiValidator<DifferentContext>
    {
        public Task<PreAiValidationResult> ValidateAsync(DifferentContext context, CancellationToken cancellationToken)
            => Task.FromResult(PreAiValidationResult.Pass());
    }

    private sealed class WarningValidator : IPreAiValidator<TestContext>
    {
        public Task<PreAiValidationResult> ValidateAsync(TestContext context, CancellationToken cancellationToken)
            => Task.FromResult(new PreAiValidationResult(
                true,
                new[]
                {
                    new ValidationError("name", "Name should be clearer.", ValidationSeverity.Warning)
                }));
    }

    private sealed class ErrorValidator : IPreAiValidator<TestContext>
    {
        public Task<PreAiValidationResult> ValidateAsync(TestContext context, CancellationToken cancellationToken)
            => Task.FromResult(new PreAiValidationResult(
                false,
                new[]
                {
                    new ValidationError("description", "Description is required.", ValidationSeverity.Error)
                }));
    }
}
