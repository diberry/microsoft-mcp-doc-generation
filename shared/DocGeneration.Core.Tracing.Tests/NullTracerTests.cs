using FluentAssertions;
using Xunit;

namespace DocGeneration.Core.Tracing.Tests;

public sealed class NullTracerTests
{
    [Fact]
    public async Task AllMethods_AreNoOps()
    {
        var tracer = NullTracer.Instance;
        var action = async () =>
        {
            using var handle = tracer.StartStep("fetch", StepClassification.Deterministic);
            handle.Complete("done");
            handle.Fail("ignored");
            tracer.RecordAiCall(new AiInteractionRecord
            {
                SkillOrToolName = "tool",
                Operation = "chat",
                SystemPrompt = "system",
                UserPrompt = "user",
                ResponseContent = "response",
                Model = "gpt-4o-mini",
                DurationMs = 1,
                RetryCount = 0
            });
            await tracer.FlushAsync(Path.Combine(AppContext.BaseDirectory, "null-tracer-output"));
        };

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task FlushAsync_CompletesImmediately()
    {
        var task = NullTracer.Instance.FlushAsync(Path.Combine(AppContext.BaseDirectory, "null-tracer-output"));

        task.IsCompleted.Should().BeTrue();
        await task;
    }

    [Fact]
    public void StepHandle_DisposeIsSafe()
    {
        var handle = NullTracer.Instance.StartStep("fetch", StepClassification.Deterministic);

        var action = () => handle.Dispose();

        action.Should().NotThrow();
    }
}
