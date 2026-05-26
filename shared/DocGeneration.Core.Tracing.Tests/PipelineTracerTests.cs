using System.Text.Json;
using DocGeneration.Core.Tracing.Models;
using FluentAssertions;
using Xunit;

namespace DocGeneration.Core.Tracing.Tests;

public sealed class PipelineTracerTests : IDisposable
{
    private readonly string _outputDirectory = Path.Combine(AppContext.BaseDirectory, "test-results", $"pipeline-tracer-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_outputDirectory))
        {
            Directory.Delete(_outputDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task StartStep_ReturnsHandle_AndCompleteSetsStatus()
    {
        var tracer = new PipelineTracer("skills-generation");

        using var handle = tracer.StartStep("fetch", StepClassification.Deterministic, "azure-functions", "read config");

        handle.Should().NotBeNull();
        handle.Complete("fetched metadata");

        var trace = await FlushAndReadTraceAsync(tracer);
        var step = trace.Steps.Should().ContainSingle().Subject;

        step.Status.Should().Be(StepStatus.Completed);
        step.StepName.Should().Be("fetch");
        step.OutputSummary.Should().Be("fetched metadata");
    }

    [Fact]
    public async Task StartStep_AndFail_SetsFailedStatus()
    {
        var tracer = new PipelineTracer("mcp");

        using var handle = tracer.StartStep("rewrite", StepClassification.AI, "storage");
        handle.Fail("model unavailable");

        var trace = await FlushAndReadTraceAsync(tracer);
        var step = trace.Steps.Should().ContainSingle().Subject;

        step.Status.Should().Be(StepStatus.Failed);
        step.Error.Should().Be("model unavailable");
    }

    [Fact]
    public async Task DisposeWithoutCompleteOrFail_MarksStepIncomplete()
    {
        var tracer = new PipelineTracer("mcp");

        var handle = tracer.StartStep("cleanup", StepClassification.Hybrid, "monitor");
        handle.Dispose();

        var trace = await FlushAndReadTraceAsync(tracer);
        var step = trace.Steps.Should().ContainSingle().Subject;

        step.Status.Should().Be(StepStatus.Incomplete);
        step.Error.Should().Be("Disposed without Complete() or Fail().");
    }

    [Fact]
    public async Task RecordAiCall_StoresInteraction()
    {
        var tracer = new PipelineTracer("skills");

        tracer.RecordAiCall(new AiInteractionRecord
        {
            SkillOrToolName = "llm-rewrite-intro",
            Operation = "rewrite",
            SystemPrompt = "system",
            UserPrompt = "user",
            ResponseContent = "response",
            Model = "gpt-4o-mini",
            TotalTokens = 42,
            DurationMs = 250,
            RetryCount = 1,
            Timestamp = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)
        });

        await tracer.FlushAsync(_outputDirectory);
        var interactions = await ReadAiInteractionsAsync();

        var interaction = interactions.Should().ContainSingle().Subject;
        interaction.SkillOrToolName.Should().Be("llm-rewrite-intro");
        interaction.TotalTokens.Should().Be(42);
    }

    [Fact]
    public async Task SequenceNumbers_AreMonotonicallyIncreasing()
    {
        var tracer = new PipelineTracer("mcp");

        using (var first = tracer.StartStep("fetch", StepClassification.Deterministic))
        {
            first.Complete();
        }

        tracer.RecordAiCall(new AiInteractionRecord
        {
            SkillOrToolName = "summary",
            Operation = "chat",
            SystemPrompt = "system",
            UserPrompt = "user",
            ResponseContent = "response",
            Model = "gpt-4o-mini",
            DurationMs = 10,
            RetryCount = 0
        });

        using (var second = tracer.StartStep("publish", StepClassification.Hybrid))
        {
            second.Fail("boom");
        }

        var trace = await FlushAndReadTraceAsync(tracer);
        var interactions = await ReadAiInteractionsAsync();

        var sequenceNumbers = new[]
        {
            trace.Steps[0].SequenceNumber,
            interactions[0].SequenceNumber,
            trace.Steps[1].SequenceNumber
        };

        sequenceNumbers.Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task FlushAsync_WritesAllArtifacts()
    {
        var tracer = new PipelineTracer("mcp");

        using (var handle = tracer.StartStep("fetch", StepClassification.Deterministic))
        {
            handle.Complete();
        }

        await tracer.FlushAsync(_outputDirectory);

        File.Exists(Path.Combine(_outputDirectory, "pipeline-trace.json")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDirectory, "ai-interactions.json")).Should().BeTrue();
        File.Exists(Path.Combine(_outputDirectory, "summary.md")).Should().BeTrue();
    }

    [Fact]
    public async Task MultipleConcurrentStartStepCalls_AreThreadSafe()
    {
        var tracer = new PipelineTracer("mcp");

        await Task.WhenAll(Enumerable.Range(1, 50).Select(index => Task.Run(() =>
        {
            using var handle = tracer.StartStep($"step-{index}", StepClassification.Deterministic, $"target-{index}");
            handle.Complete($"output-{index}");
        })));

        var trace = await FlushAndReadTraceAsync(tracer);

        trace.Steps.Should().HaveCount(50);
        trace.Steps.Select(step => step.SequenceNumber).Should().OnlyHaveUniqueItems();
        trace.Steps.Should().OnlyContain(step => step.Status == StepStatus.Completed);
    }

    private async Task<PipelineTrace> FlushAndReadTraceAsync(PipelineTracer tracer)
    {
        await tracer.FlushAsync(_outputDirectory);
        var json = await File.ReadAllTextAsync(Path.Combine(_outputDirectory, "pipeline-trace.json"));
        return JsonSerializer.Deserialize<PipelineTrace>(json, TraceWriter.TraceJsonSerializerOptions)!;
    }

    private async Task<IReadOnlyList<AiInteraction>> ReadAiInteractionsAsync()
    {
        var json = await File.ReadAllTextAsync(Path.Combine(_outputDirectory, "ai-interactions.json"));
        return JsonSerializer.Deserialize<IReadOnlyList<AiInteraction>>(json, TraceWriter.TraceJsonSerializerOptions)!;
    }
}
