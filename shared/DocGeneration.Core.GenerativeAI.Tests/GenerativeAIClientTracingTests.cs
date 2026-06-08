using DocGeneration.Core.Tracing;
using Microsoft.Extensions.AI;
using Xunit;

namespace GenerativeAI.Tests;

public class GenerativeAIClientTracingTests
{
    [Fact]
    public async Task GetChatCompletionAsync_WithTracer_RecordsAiCallWithContextAndModelName()
    {
        var tracer = new RecordingTracer();
        var client = new GenerativeAI.GenerativeAIClient(new StubChatClient("trace me"), tracer, "gpt-test-model");

        var response = await client.GetChatCompletionAsync(
            "system prompt",
            "user prompt",
            toolOrNamespace: "storage",
            operation: "GenerateExamplePrompts");

        Assert.Equal("trace me", response);
        var record = Assert.Single(tracer.Records);
        Assert.Equal("storage", record.SkillOrToolName);
        Assert.Equal("GenerateExamplePrompts", record.Operation);
        Assert.Equal("system prompt", record.SystemPrompt);
        Assert.Equal("user prompt", record.UserPrompt);
        Assert.Equal("trace me", record.ResponseContent);
        Assert.Equal("gpt-test-model", record.Model);
        Assert.Equal(0, record.RetryCount);
        Assert.True(record.DurationMs >= 0);
    }

    [Fact]
    public async Task GetChatCompletionAsync_WithoutContext_UsesDefaultTraceValues()
    {
        var tracer = new RecordingTracer();
        var client = new GenerativeAI.GenerativeAIClient(new StubChatClient("default trace"), tracer);

        await client.GetChatCompletionAsync("system", "user");

        var record = Assert.Single(tracer.Records);
        Assert.Equal("unknown", record.SkillOrToolName);
        Assert.Equal("GetChatCompletion", record.Operation);
        Assert.Equal("unknown", record.Model);
    }

    private sealed class StubChatClient(string responseText) : IChatClient
    {
        public ChatClientMetadata Metadata => new("test");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))
            {
                FinishReason = ChatFinishReason.Stop
            };

            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }

    private sealed class RecordingTracer : IPipelineTracer
    {
        public List<AiInteractionRecord> Records { get; } = [];

        public IStepHandle StartStep(string stepName, StepClassification stepType, string? targetName = null, string? inputSummary = null)
            => throw new NotSupportedException();

        public void RecordAiCall(AiInteractionRecord record) => Records.Add(record);

        public Task FlushAsync(string outputDirectory, CancellationToken ct = default) => Task.CompletedTask;
    }
}
