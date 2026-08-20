using Azure;
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

    [Fact]
    public async Task GetChatCompletionAsync_Success_LogsHttpStatusAndCallContext()
    {
        var statusLines = new List<string>();
        var client = new GenerativeAI.GenerativeAIClient(
            new StubChatClient("ok"),
            statusLogger: statusLines.Add);

        await client.GetChatCompletionAsync(
            "system",
            "user",
            toolOrNamespace: "containerapps",
            operation: "AiEndpointHealthCheck");

        var line = Assert.Single(statusLines);
        Assert.Contains("status=200", line);
        Assert.Contains("outcome=success", line);
        Assert.Contains("operation=AiEndpointHealthCheck", line);
        Assert.Contains("target=containerapps", line);
    }

    [Fact]
    public async Task GetChatCompletionAsync_HttpFailure_LogsStatusBeforeRethrowing()
    {
        var statusLines = new List<string>();
        var expected = new RequestFailedException(401, "Unauthorized");
        var client = new GenerativeAI.GenerativeAIClient(
            new ThrowingChatClient(expected),
            statusLogger: statusLines.Add);

        var actual = await Assert.ThrowsAsync<RequestFailedException>(
            () => client.GetChatCompletionAsync(
                "system",
                "user",
                toolOrNamespace: "storage",
                operation: "GenerateTool"));

        Assert.Same(expected, actual);
        var line = Assert.Single(statusLines);
        Assert.Contains("status=401", line);
        Assert.Contains("outcome=failure", line);
        Assert.Contains("operation=GenerateTool", line);
        Assert.Contains("target=storage", line);
    }

    [Fact]
    public async Task GetChatCompletionAsync_TruncatedResponse_ThrowsWithPartialResponseContent()
    {
        const string partialResponse = """{"genai-serviceOverview":"partial""";
        var client = new GenerativeAI.GenerativeAIClient(
            new StubChatClient(partialResponse, ChatFinishReason.Length));

        var exception = await Assert.ThrowsAsync<AiResponseTruncatedException>(
            () => client.GetChatCompletionAsync("system", "user", maxTokens: 500));

        Assert.Equal(partialResponse, exception.ResponseContent);
        Assert.Equal(500, exception.MaxOutputTokens);
    }

    private sealed class StubChatClient(
        string responseText,
        ChatFinishReason? finishReason = null) : IChatClient
    {
        public ChatClientMetadata Metadata => new("test");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText))
            {
                FinishReason = finishReason ?? ChatFinishReason.Stop
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

    private sealed class ThrowingChatClient(Exception exception) : IChatClient
    {
        public ChatClientMetadata Metadata => new("test");

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> chatMessages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromException<ChatResponse>(exception);

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
